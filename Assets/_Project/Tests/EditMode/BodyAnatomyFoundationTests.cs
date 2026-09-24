using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BodyAnatomyFoundationTests
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
        public void PrototypeCatalog_ResolvesCanonicalAnatomiesAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<AnatomyDefinition>(registry, "anatomy.human");
            AssertResolves<AnatomyDefinition>(registry, "anatomy.basic-construct");
            AssertResolves<AnatomyDefinition>(registry, "anatomy.basic-spirit");
            Assert.That(registry.TryGet("species.human", out SpeciesDefinition human), Is.True);
            Assert.That(human.AnatomyDefinition.Id, Is.EqualTo("anatomy.human"));
            Assert.That(registry.TryGet("species.undead-human", out SpeciesDefinition undead), Is.True);
            Assert.That(undead.AnatomyDefinition.Id, Is.EqualTo("anatomy.human"));
        }

        [Test]
        public void HumanAnatomy_RuntimeContainsRootRegionsLimbsOrgansAndVitalStructures()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.anatomy.human", "person.anatomy");

            BodyOperationResult result = body.AssignSpecies("species.human");
            Assert.That(result.Succeeded, Is.True, result.Message);

            AnatomySnapshot snapshot = body.CreateAnatomySnapshot();
            Assert.That(snapshot.Readiness, Is.EqualTo(AnatomyReadinessState.Ready));
            Assert.That(snapshot.RootNodeId, Is.EqualTo("structure.human-root"));
            AssertHasNodes(snapshot, "region.head", "region.torso", "region.arm.left", "region.arm.right", "region.leg.left", "region.leg.right");
            AssertHasNodes(snapshot, "part.arm.left", "part.arm.right", "part.leg.left", "part.leg.right", "part.hand.left", "part.hand.right");
            AssertHasNodes(snapshot, "organ.brain", "organ.heart", "organ.lung.left", "organ.lung.right");
            Assert.That(snapshot.VitalStructures.Select(node => node.NodeId), Does.Contain("organ.brain"));
            Assert.That(snapshot.TargetableRegions.Select(node => node.NodeId), Does.Contain("region.head"));
            Assert.That(Get(snapshot, "part.arm.left").BodySide, Is.EqualTo(AnatomyBodySide.Left));
            Assert.That(Get(snapshot, "part.arm.right").BodySide, Is.EqualTo(AnatomyBodySide.Right));
            Assert.That(Get(snapshot, "organ.brain").InternalStructure, Is.True);
            Assert.That(Get(snapshot, "organ.brain").Targetable, Is.False);
            Assert.That(Get(snapshot, "part.hand.left").EquipmentTagIds, Does.Contain("equipment.hand-compatible"));
        }

        [Test]
        public void ConstructAndSpiritAnatomies_AreNotHumanoidHardcoded()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.anatomy.nonhuman", "person.anatomy");

            Assert.That(body.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            AnatomySnapshot construct = body.CreateAnatomySnapshot();
            Assert.That(construct.AnatomyDefinitionId, Is.EqualTo("anatomy.basic-construct"));
            AssertHasNodes(construct, "structure.construct-root", "part.chassis", "part.manipulator.left", "part.manipulator.right", "core.power");
            Assert.That(construct.Nodes.Any(node => node.NodeId == "organ.heart"), Is.False);
            Assert.That(Get(construct, "core.power").Vital, Is.True);

            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            AnatomySnapshot spirit = body.CreateAnatomySnapshot();
            Assert.That(spirit.AnatomyDefinitionId, Is.EqualTo("anatomy.basic-spirit"));
            AssertHasNodes(spirit, "structure.spirit-root", "region.essence", "essence.aura", "core.spiritual");
            Assert.That(spirit.Nodes.Any(node => node.NodeId == "part.arm.left"), Is.False);
            Assert.That(spirit.Nodes.All(node => !node.Corporeal), Is.True);
        }

        [Test]
        public void RuntimeNodeIds_AreExactBodyOwnedDeterministicAndStableAcrossRebuild()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.anatomy.stable", "person.anatomy");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);

            string[] before = body.CreateAnatomySnapshot().Nodes.Select(node => node.RuntimeNodeId).ToArray();
            long revisionBefore = body.Anatomy.AnatomyRevision;

            BodyOperationResult rebuild = body.RebuildAnatomy();
            Assert.That(rebuild.Succeeded, Is.True, rebuild.Message);

            string[] after = body.CreateAnatomySnapshot().Nodes.Select(node => node.RuntimeNodeId).ToArray();
            Assert.That(after, Is.EqualTo(before));
            Assert.That(after.All(id => id.Contains("actor.runtime.anatomy.stable", StringComparison.Ordinal)), Is.True);
            Assert.That(body.Anatomy.AnatomyRevision, Is.GreaterThan(revisionBefore));
        }

        [Test]
        public void SnapshotsAndQueries_AreReadOnlyAndImmutable()
        {
            GameObject owner = CreateOwner("Anatomy Query Test");
            DefinitionRegistry registry = LoadRegistry();
            ConfigureSubsystems(owner, registry, "actor.runtime.anatomy.query", "person.anatomy");
            owner.AddComponent<CharacterSystemCoordinator>();
            CharacterSystemCoordinator coordinator = owner.GetComponent<CharacterSystemCoordinator>();
            ActorBodyRuntime body = owner.GetComponent<ActorBodyRuntime>();
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(coordinator.InitializeFromRegistry(registry, false), Is.True);

            long anatomyRevision = coordinator.Body.Anatomy.AnatomyRevision;
            AnatomySnapshot snapshot = coordinator.Query.GetAnatomySnapshot();
            Assert.That(snapshot.Nodes, Is.Not.Empty);
            Assert.That(coordinator.Body.Anatomy.AnatomyRevision, Is.EqualTo(anatomyRevision));
            Assert.That(() => ((IList<AnatomyNodeSnapshot>)snapshot.Nodes).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(coordinator.Query.IsAnatomyReady(), Is.True);
            Assert.That(coordinator.Query.IsStructurePresent("organ.heart"), Is.True);
            Assert.That(coordinator.Query.GetAnatomyNode("organ.heart").Vital, Is.True);
        }

        [Test]
        public void OptionalPresenceOverride_PersistsAndRestoreDoesNotDuplicateNodes()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateBodyRuntime(registry, "actor.runtime.anatomy.restore", "person.anatomy");
            Assert.That(original.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(original.SetAnatomyPresenceOverride("part.tail.optional", AnatomyPresenceState.Present).Succeeded, Is.True);
            BodySaveData saveData = original.CreateSaveData();
            string[] originalIds = original.CreateAnatomySnapshot().Nodes.Select(node => node.RuntimeNodeId).ToArray();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.anatomy.restore", "person.anatomy");
            int eventCount = 0;
            restored.BodyChanged += (_, _, _) => eventCount++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.anatomy.restore", "person.anatomy", restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(eventCount, Is.EqualTo(0));
            AnatomySnapshot snapshot = restored.CreateAnatomySnapshot();
            Assert.That(Get(snapshot, "part.tail.optional").Presence, Is.EqualTo(AnatomyPresenceState.Present));
            Assert.That(snapshot.Nodes.Select(node => node.RuntimeNodeId).ToArray(), Is.EqualTo(originalIds));
            Assert.That(snapshot.Nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(snapshot.Nodes.Count));
        }

        [Test]
        public void RestoreRejectsStaleBodyAndWrongAnatomyDefinition()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.anatomy.reject", "person.anatomy");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            BodySaveData saveData = body.CreateSaveData();

            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.replacement", "person.anatomy", out string staleFailure), Is.False);
            Assert.That(staleFailure, Does.Contain("does not match"));

            saveData.anatomy.anatomyDefinitionId = "anatomy.basic-spirit";
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.anatomy.reject", "person.anatomy", out string anatomyFailure), Is.False);
            Assert.That(anatomyFailure, Does.Contain("does not match Species anatomy"));
        }

        [Test]
        public void RuntimeAnatomyCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/Anatomy";
            foreach (string file in Directory.GetFiles(runtimeFolder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.Development"), file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.UI"), file);
                Assert.That(text, Does.Not.Contain("UnityEditor"), file);
                Assert.That(text, Does.Not.Contain("Prototype"), file);
            }
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = CreateOwner("Anatomy Test Body");
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
            owner.AddComponent<UnityIsekaiGame.Capabilities.CharacterCapabilityCollection>();
            owner.AddComponent<ActorBodyRuntime>();
            return owner;
        }

        private static void ConfigureSubsystems(GameObject owner, DefinitionRegistry registry, string actorBodyId, string personId)
        {
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

        private static void AssertHasNodes(AnatomySnapshot snapshot, params string[] nodeIds)
        {
            foreach (string nodeId in nodeIds)
            {
                Assert.That(snapshot.Nodes.Any(node => node.NodeId == nodeId), Is.True, $"Missing anatomy node '{nodeId}'.");
            }
        }

        private static AnatomyNodeSnapshot Get(AnatomySnapshot snapshot, string nodeId)
        {
            AnatomyNodeSnapshot node = snapshot.Nodes.FirstOrDefault(candidate => candidate.NodeId == nodeId);
            Assert.That(node, Is.Not.Null, $"Missing anatomy node '{nodeId}'.");
            return node;
        }
    }
}
