using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Integration;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Tests
{
    public sealed class BodyBiologyIntegrationFinalizationTests
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
                    Object.DestroyImmediate(obj);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Facade_CapturesCoherentAggregateSnapshot()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.body-biology.snapshot", "person.body-biology");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BodyBiologyFacade facade = new BodyBiologyFacade(body);

            BodyBiologySnapshot snapshot = facade.CaptureSnapshot();

            Assert.That(snapshot.Ready, Is.True, string.Join(" ", snapshot.Diagnostics));
            Assert.That(snapshot.ActorBodyId, Is.EqualTo(body.ActorBodyId));
            Assert.That(snapshot.PersonId, Is.EqualTo(body.PersonId));
            Assert.That(snapshot.SpeciesId, Is.EqualTo("species.human"));
            Assert.That(snapshot.Body, Is.Not.Null);
            Assert.That(snapshot.BiologicalConditions, Is.Not.Null);
            Assert.That(snapshot.Transformation, Is.Not.Null);
            Assert.That(snapshot.Revisions.BodyRevision, Is.EqualTo(snapshot.Body.BodyRevision));
            Assert.That(snapshot.Revisions.BiologicalConditionRevision, Is.EqualTo(snapshot.BiologicalConditions.BiologicalConditionRevision));
            Assert.That(snapshot.Revisions.TransformationRevision, Is.EqualTo(snapshot.Transformation.TransformationRevision));
        }

        [Test]
        public void Validation_ReportsMissingBodyClearly()
        {
            BodyBiologyValidationResult result = new BodyBiologyFacade(null).Validate();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(BodyBiologyValidationCode.MissingActorBody).Or.EqualTo(BodyBiologyValidationCode.MissingBody).Or.EqualTo(BodyBiologyValidationCode.RuntimeNotReady));
            Assert.That(result.Message, Does.Contain("Body"));
        }

        [Test]
        public void PreviewAdvance_DoesNotMutateRevisions()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.body-biology.preview", "person.body-biology");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.body-biology.preview.fever", "condition.biology.prototype-fever-response"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            BodyBiologyFacade facade = new BodyBiologyFacade(body);
            BodyBiologySnapshot before = facade.CaptureSnapshot();

            BodyBiologyAdvanceResult result = facade.PreviewAdvance(new BodyBiologyAdvanceRequest(body.ActorBodyId, 600f, "tx.body-biology.preview", damageHealing: new DamageHealingService()));
            BodyBiologySnapshot after = facade.CaptureSnapshot();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(after.Revisions.BodyRevision, Is.EqualTo(before.Revisions.BodyRevision));
            Assert.That(after.Revisions.ConditionRevision, Is.EqualTo(before.Revisions.ConditionRevision));
            Assert.That(after.Revisions.VitalRevision, Is.EqualTo(before.Revisions.VitalRevision));
            Assert.That(after.Revisions.HazardRevision, Is.EqualTo(before.Revisions.HazardRevision));
            Assert.That(after.Revisions.RecoveryRevision, Is.EqualTo(before.Revisions.RecoveryRevision));
            Assert.That(after.Revisions.BiologicalConditionRevision, Is.EqualTo(before.Revisions.BiologicalConditionRevision));
        }

        [Test]
        public void Advance_UsesDeterministicOrderAndDuplicateTransactionIsIdempotent()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.body-biology.advance", "person.body-biology");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(body.BiologicalConditions.ApplyExposure(Exposure(body, "tx.body-biology.advance.fever", "condition.biology.prototype-fever-response"), body.CreateSnapshot(), body.BiologicalCompatibility).Succeeded, Is.True);
            BodyBiologyFacade facade = new BodyBiologyFacade(body);

            BodyBiologyAdvanceResult first = facade.Advance(new BodyBiologyAdvanceRequest(body.ActorBodyId, 600f, "tx.body-biology.advance", damageHealing: new DamageHealingService()));
            BodyBiologySnapshot afterFirst = facade.CaptureSnapshot();
            BodyBiologyAdvanceResult duplicate = facade.Advance(new BodyBiologyAdvanceRequest(body.ActorBodyId, 600f, "tx.body-biology.advance", damageHealing: new DamageHealingService()));
            BodyBiologySnapshot afterDuplicate = facade.CaptureSnapshot();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Steps.Select(step => step.StepId), Is.EqualTo(new[]
            {
                BodyBiologyAdvanceSteps.Conditions,
                BodyBiologyAdvanceSteps.Hazards,
                BodyBiologyAdvanceSteps.Vitals,
                BodyBiologyAdvanceSteps.Recovery
            }));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(afterDuplicate.Revisions.VitalRevision, Is.EqualTo(afterFirst.Revisions.VitalRevision));
            Assert.That(afterDuplicate.Revisions.HazardRevision, Is.EqualTo(afterFirst.Revisions.HazardRevision));
            Assert.That(afterDuplicate.Revisions.RecoveryRevision, Is.EqualTo(afterFirst.Revisions.RecoveryRevision));
            Assert.That(afterDuplicate.Revisions.BiologicalConditionRevision, Is.EqualTo(afterFirst.Revisions.BiologicalConditionRevision));
        }

        [Test]
        public void RuntimeIntegrationCode_HasNoDevelopmentUiOrEditorDependency()
        {
            foreach (string file in Directory.GetFiles("Assets/_Project/Runtime/Actors/Beings/Biology/Integration", "*.cs"))
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.Development"), file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.UI"), file);
                Assert.That(text, Does.Not.Contain("UnityEditor"), file);
                Assert.That(text, Does.Not.Contain("PrototypeTestLab"), file);
            }
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Body Biology Integration Test Body");
            createdObjects.Add(owner);
            owner.AddComponent<CharacterAttributes>();
            owner.AddComponent<CalculatedStatCollection>();
            owner.AddComponent<CharacterResourceCollection>();
            owner.AddComponent<CharacterTraitCollection>();
            owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            WorldEntityIdentity identity = owner.AddComponent<WorldEntityIdentity>();
            owner.AddComponent<ActorBodyRuntime>();

            CharacterAttributes attributes = owner.GetComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();
            CharacterResourceCollection resources = owner.GetComponent<CharacterResourceCollection>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            UnityIsekaiGame.Capabilities.CharacterCapabilityCollection capabilities = owner.GetComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            Assert.That(identity.TrySetAuthoredIdentity(actorBodyId.Replace('.', '-'), "scene.test", PersistenceScope.RegionOrScene, "test.body-biology", out string identityFailure), Is.True, identityFailure);
            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            resources.Configure(registry, stats, personId);
            capabilities.Configure(registry);
            traits.Configure(registry, stats, null, capabilities, personId);
            body.Configure(registry, actorBodyId, personId, traits, stats, capabilityCollection: capabilities);
            return body;
        }

        private static BiologicalConditionExposureRequest Exposure(ActorBodyRuntime body, string transactionId, string conditionDefinitionId)
        {
            BodySnapshot snapshot = body.CreateSnapshot();
            return new BiologicalConditionExposureRequest(
                body.ActorBodyId,
                conditionDefinitionId,
                transactionId,
                BiologicalExposureRoute.Scripted,
                10f,
                sourceId: "test.body-biology",
                sourceBodyId: body.ActorBodyId,
                sourceEventId: transactionId,
                sourceCategory: BiologicalConditionSourceCategory.Development,
                expectedBodyRevision: snapshot.BodyRevision,
                expectedAnatomyRevision: snapshot.Anatomy == null ? 0L : snapshot.Anatomy.AnatomyRevision,
                expectedConditionRevision: snapshot.Condition == null ? 0L : snapshot.Condition.ConditionRevision,
                expectedVitalRevision: snapshot.VitalProcesses == null ? 0L : snapshot.VitalProcesses.VitalRevision,
                expectedHazardRevision: snapshot.BiologicalHazards == null ? 0L : snapshot.BiologicalHazards.HazardRevision,
                expectedCompatibilityRevision: snapshot.BiologicalCompatibility == null ? 0L : snapshot.BiologicalCompatibility.CompatibilityRevision);
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
    }
}
