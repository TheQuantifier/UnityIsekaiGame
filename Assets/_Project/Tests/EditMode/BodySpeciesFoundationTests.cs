using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BodySpeciesFoundationTests
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
        public void CanonicalBodySpeciesDefinitionsResolveFromPrototypeCatalog()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<BiologicalClassificationDefinition>(registry, "biology.classification.living");
            AssertResolves<BiologicalClassificationDefinition>(registry, "biology.classification.undead");
            AssertResolves<BiologicalClassificationDefinition>(registry, "biology.classification.construct");
            AssertResolves<BiologicalClassificationDefinition>(registry, "biology.classification.spirit");
            AssertResolves<BodyFormDefinition>(registry, "body-form.humanoid");
            AssertResolves<BodyFormDefinition>(registry, "body-form.construct");
            AssertResolves<BodyFormDefinition>(registry, "body-form.incorporeal");
            AssertResolves<SpeciesDefinition>(registry, "species.human");
            AssertResolves<SpeciesDefinition>(registry, "species.undead-human");
            AssertResolves<SpeciesDefinition>(registry, "species.basic-construct");
            AssertResolves<SpeciesDefinition>(registry, "species.basic-spirit");
            AssertResolves<UnityIsekaiGame.Capabilities.CapabilityDefinition>(registry, "capability.can.die");
        }

        [Test]
        public void AssignSpeciesAppliesOwnedTraitsCapabilitiesAndStatSourcesOnce()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.test.body", "person.test");

            BodyOperationResult result = body.AssignSpecies("species.human");
            Assert.That(result.Succeeded, Is.True, result.Message);

            BodySnapshot snapshot = body.CreateSnapshot();
            Assert.That(snapshot.SpeciesId, Is.EqualTo("species.human"));
            Assert.That(snapshot.BiologicalClassificationId, Is.EqualTo("biology.classification.living"));
            Assert.That(snapshot.BodyFormId, Is.EqualTo("body-form.humanoid"));
            Assert.That(snapshot.RequiresBreathing, Is.True);
            Assert.That(snapshot.HasBlood, Is.True);
            Assert.That(snapshot.CanDie, Is.True);
            Assert.That(snapshot.SpeciesOwnedTraits.Select(trait => trait.TraitId), Does.Contain("trait.living"));
            Assert.That(snapshot.BiologicalStatContributions.Select(contribution => contribution.SourceId), Does.Contain("species.human"));
            Assert.That(snapshot.BiologicalCapabilities.Select(capability => capability.CapabilityId), Does.Contain("capability.can.die"));

            long revision = body.BodyRevision;
            BodyOperationResult duplicate = body.AssignSpecies("species.human");
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(body.BodyRevision, Is.EqualTo(revision));
            Assert.That(body.CreateSnapshot().BiologicalCapabilities.Count(capability => capability.CapabilityId == BiologyCapabilityIds.IsLiving), Is.EqualTo(1));
        }

        [Test]
        public void SpeciesCapabilityRemovalPreservesOtherSourcesAndDoesNotDuplicate()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateOwner("Body Capability Aggregation Test");
            ConfigureSubsystems(owner, registry, "actor.runtime.test.aggregate", "person.test");
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            CharacterCapabilityCollection capabilities = owner.GetComponent<CharacterCapabilityCollection>();

            Assert.That(capabilities.Add(new RuntimeCapabilityContribution
            {
                capabilityId = ActorLifecycleCapabilityIds.CanDie,
                valueType = (int)CapabilityValueType.Boolean,
                boolValue = true,
                aggregationPolicy = (int)CapabilityAggregationPolicy.BooleanAny,
                sourceCategory = (int)CapabilitySourceCategory.Development,
                sourceId = "test.external-source",
                entryId = "test.external.can-die"
            }), Is.True);

            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            AssertCanDieSources(capabilities, expectedCount: 2, requiredSource: "test.external-source", forbiddenSource: string.Empty);

            Assert.That(body.AssignSpecies("species.human").Duplicate, Is.True);
            AssertCanDieSources(capabilities, expectedCount: 2, requiredSource: "test.external-source", forbiddenSource: string.Empty);

            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            AssertCanDieSources(
                capabilities,
                expectedCount: 2,
                requiredSource: "test.external-source",
                forbiddenSource: "body.species.actor.runtime.test.aggregate.species.human");
        }

        [Test]
        public void AssignSpeciesClearsStaleTargetStatSourcesBeforeReapplying()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateOwner("Body Stale Stat Source Test");
            ConfigureSubsystems(owner, registry, "actor.runtime.test.stale-stat", "person.test");
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();

            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);

            bool added = stats.AddContribution(new RuntimeCalculatedStatContribution
            {
                contributionId = "test.stale-human-health",
                statId = "calculated-stat.maximum-health",
                sourceId = "body.species.actor.runtime.test.stale-stat.species.human",
                sourceCategory = (int)CalculatedStatContributionSourceCategory.Species,
                kind = (int)CalculatedStatContributionKind.Flat,
                direction = (int)CalculatedStatContributionDirection.Improve,
                magnitude = 5f
            }, out string failureReason);
            Assert.That(added, Is.True, failureReason);

            body.Configure(registry, "actor.runtime.test.stale-stat", "person.test", owner.GetComponent<CharacterTraitCollection>(), stats, restoring: true);
            BodyOperationResult result = body.AssignSpecies("species.human");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo("species.human"));
        }

        [Test]
        public void PreviewSpeciesAssignmentDoesNotMutateBody()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.test.preview", "person.test");

            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            long revision = body.BodyRevision;

            BodyOperationResult preview = body.PreviewAssignSpecies("species.undead-human");
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(body.SpeciesDefinitionId, Is.EqualTo("species.human"));
            Assert.That(body.BodyRevision, Is.EqualTo(revision));
            Assert.That(body.CreateSnapshot().BiologicalClassificationId, Is.EqualTo("biology.classification.living"));
        }

        [Test]
        public void RestoreRejectsMismatchedActorBodyAndUnknownSpecies()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.test.restore", "person.test");

            Assert.That(body.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            BodySaveData saveData = body.CreateSaveData();

            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.replacement", "person.test", out string actorFailure), Is.False);
            Assert.That(actorFailure, Does.Contain("does not match"));

            saveData.speciesDefinitionId = "species.missing";
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.test.restore", "person.test", out string speciesFailure), Is.False);
            Assert.That(speciesFailure, Does.Contain("unknown Species"));
        }

        [Test]
        public void RestoreReappliesSpeciesSourcesSilently()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateBodyRuntime(registry, "actor.runtime.test.restore-silent", "person.restore");

            Assert.That(original.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            BodySaveData saveData = original.CreateSaveData();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.test.restore-silent", "person.restore");
            int bodyChangedEvents = 0;
            restored.BodyChanged += (_, _, _) => bodyChangedEvents++;

            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.test.restore-silent", "person.restore", restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(bodyChangedEvents, Is.EqualTo(0));
            BodySnapshot snapshot = restored.CreateSnapshot();
            Assert.That(snapshot.SpeciesId, Is.EqualTo("species.basic-construct"));
            Assert.That(snapshot.AcceptsRepair, Is.True);
            Assert.That(snapshot.BodyRevision, Is.EqualTo(saveData.bodyRevision));
        }

        [Test]
        public void QueryServiceExposesBodySpeciesWithoutOwningBiologyState()
        {
            DefinitionRegistry registry = LoadRegistry();
            GameObject owner = CreateOwner("Body Query Test");
            owner.AddComponent<CharacterSystemCoordinator>();
            CharacterSystemCoordinator coordinator = owner.GetComponent<CharacterSystemCoordinator>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            ConfigureSubsystems(owner, registry, "actor.runtime.query", "person.query");
            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            Assert.That(coordinator.InitializeFromRegistry(registry, false), Is.True);

            CharacterQueryService query = coordinator.Query;
            Assert.That(query.IsBodyReady(), Is.True);
            Assert.That(query.GetSpecies().Id, Is.EqualTo("species.basic-spirit"));
            Assert.That(query.GetBiologicalClassification().Id, Is.EqualTo("biology.classification.spirit"));
            Assert.That(query.GetBodyForm().Id, Is.EqualTo("body-form.incorporeal"));
            Assert.That(query.HasBiologicalCapability(BiologyCapabilityIds.IsSpirit), Is.True);
            Assert.That(query.ValidateBody().Succeeded, Is.True);
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = CreateOwner("Body Species Test");
            ConfigureSubsystems(owner, registry, actorBodyId, personId);
            return owner.GetComponent<ActorBodyRuntime>();
        }

        private GameObject CreateOwner(string name)
        {
            GameObject owner = new GameObject(name);
            createdObjects.Add(owner);
            owner.AddComponent<CharacterAttributes>();
            owner.AddComponent<CalculatedStatCollection>();
            owner.AddComponent<CharacterTraitCollection>();
            owner.AddComponent<CharacterCapabilityCollection>();
            owner.AddComponent<ActorBodyRuntime>();
            return owner;
        }

        private static void ConfigureSubsystems(GameObject owner, DefinitionRegistry registry, string actorBodyId, string personId)
        {
            CharacterAttributes attributes = owner.GetComponent<CharacterAttributes>();
            CalculatedStatCollection stats = owner.GetComponent<CalculatedStatCollection>();
            CharacterTraitCollection traits = owner.GetComponent<CharacterTraitCollection>();
            CharacterCapabilityCollection capabilities = owner.GetComponent<CharacterCapabilityCollection>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();

            attributes.Configure(registry);
            stats.Configure(registry, attributes);
            capabilities.Configure(registry);
            traits.Configure(registry, stats, null, capabilities, personId);
            body.Configure(registry, actorBodyId, personId, traits, stats, capabilityCollection: capabilities);
        }

        private static DefinitionRegistry LoadRegistry()
        {
            DefinitionCatalog catalog = LoadCatalog();
            return catalog.CreateRegistry();
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

        private static void AssertCanDieSources(CharacterCapabilityCollection capabilities, int expectedCount, string requiredSource, string forbiddenSource)
        {
            CapabilitySnapshot snapshot = capabilities.Evaluate(ActorLifecycleCapabilityIds.CanDie);
            Assert.That(snapshot.BooleanValue, Is.True);
            Assert.That(snapshot.Sources, Has.Count.EqualTo(expectedCount));
            Assert.That(snapshot.Sources.Any(source => source.sourceId == requiredSource), Is.True);
            if (!string.IsNullOrWhiteSpace(forbiddenSource))
            {
                Assert.That(snapshot.Sources.Any(source => source.sourceId == forbiddenSource), Is.False);
            }
        }
    }
}
