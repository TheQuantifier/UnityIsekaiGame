using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BodyConditionInjuryTests
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
        public void PrototypeCatalog_ResolvesCanonicalInjuryDefinitionsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<InjuryTypeDefinition>(registry, "injury.blunt-trauma");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.bruise");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.laceration");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.puncture");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.penetrating");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.fracture");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.crush");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.burn");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.structural-rupture");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.severing");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.organ-trauma");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.core-damage");
            AssertResolves<InjuryTypeDefinition>(registry, "injury.incorporeal-disruption");
            AssertResolves<StructuralFailurePolicyDefinition>(registry, "structural-failure.disabled");
            AssertResolves<StructuralFailurePolicyDefinition>(registry, "structural-failure.destroyed");
            AssertResolves<StructuralFailurePolicyDefinition>(registry, "structural-failure.vital-structure");
        }

        [Test]
        public void HealthyRuntime_InitializesConditionForStableAnatomyNodes()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.healthy", "person.condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);

            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();
            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            Assert.That(condition.Readiness, Is.EqualTo(BodyConditionReadinessState.Ready));
            Assert.That(condition.Structures.Select(structure => structure.NodeId), Is.EquivalentTo(anatomy.Nodes.Select(node => node.NodeId)));
            Assert.That(GetStructure(condition, "part.arm.left").CurrentIntegrity, Is.EqualTo(100));
            Assert.That(GetStructure(condition, "region.head").MaximumIntegrity, Is.EqualTo(0));
            Assert.That(condition.ActiveInjuries, Is.Empty);
            Assert.That(condition.Coherent, Is.True);
        }

        [Test]
        public void PreviewLocalizedDamage_UsesSameRequestButDoesNotMutate()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.preview", "person.condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            long revisionBefore = body.Condition.ConditionRevision;
            int events = 0;
            body.Condition.ConditionChanged += (_, _, _) => events++;

            LocalizedStructuralDamageResult result = body.Condition.PreviewLocalizedDamage(Request(body, "tx.condition.preview", "injury.blunt-trauma", "part.arm.left", 12), body.CreateAnatomySnapshot(), body.BiologicalCompatibility, body.CreateSnapshot());

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(revisionBefore));
            Assert.That(body.Condition.CreateSnapshot().ActiveInjuries, Is.Empty);
            Assert.That(GetStructure(body.Condition.CreateSnapshot(), "part.arm.left").CurrentIntegrity, Is.EqualTo(100));
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void ExecuteLocalizedDamage_MutatesOnceAndRecordsInjury()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.execute", "person.condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            long revisionBefore = body.Condition.ConditionRevision;
            int events = 0;
            body.Condition.ConditionChanged += (_, _, restoring) =>
            {
                Assert.That(restoring, Is.False);
                events++;
            };

            LocalizedStructuralDamageResult result = body.Condition.ApplyLocalizedDamage(Request(body, "tx.condition.execute", "injury.laceration", "part.hand.left", 14), body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            BodyConditionSnapshot snapshot = body.Condition.CreateSnapshot();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.DamageApplied, Is.EqualTo(14));
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(revisionBefore + 1));
            Assert.That(snapshot.ActiveInjuries.Count, Is.EqualTo(1));
            Assert.That(snapshot.ActiveInjuries[0].TargetNodeId, Is.EqualTo("part.hand.left"));
            Assert.That(GetStructure(snapshot, "part.hand.left").CurrentIntegrity, Is.EqualTo(86));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateTransaction_IsIdempotentAndDoesNotApplySecondDamage()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.duplicate", "person.condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            LocalizedStructuralDamageRequest request = Request(body, "tx.condition.duplicate", "injury.blunt-trauma", "part.arm.left", 12);

            LocalizedStructuralDamageResult first = body.Condition.ApplyLocalizedDamage(request, body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            long revisionAfterFirst = body.Condition.ConditionRevision;
            LocalizedStructuralDamageResult duplicate = body.Condition.ApplyLocalizedDamage(request, body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            BodyConditionSnapshot snapshot = body.Condition.CreateSnapshot();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(revisionAfterFirst));
            Assert.That(snapshot.ActiveInjuries.Count, Is.EqualTo(1));
            Assert.That(GetStructure(snapshot, "part.arm.left").CurrentIntegrity, Is.EqualTo(88));
        }

        [Test]
        public void DuplicateTransaction_ReplaysEvenWhenOriginalTargetBecameUnavailable()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.duplicate-unavailable", "person.condition");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            LocalizedStructuralDamageRequest request = Request(body, "tx.condition.duplicate.unavailable", "injury.severing", "part.arm.left", 100);

            LocalizedStructuralDamageResult first = body.Condition.ApplyLocalizedDamage(request, body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            LocalizedStructuralDamageResult duplicate = body.Condition.ApplyLocalizedDamage(request, body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            BodyConditionSnapshot snapshot = body.Condition.CreateSnapshot();

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(GetStructure(snapshot, "part.arm.left").RuntimePresence, Is.EqualTo(RuntimeStructurePresenceState.Severed));
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(duplicate.Code, Is.EqualTo(LocalizedDamageResultCode.Duplicate));
            Assert.That(snapshot.ActiveInjuries.Count, Is.EqualTo(1));
        }

        [Test]
        public void MissingNodeAndIncompatibleInjury_FailWithoutMutation()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.condition.invalid", "person.condition");
            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            long revisionBefore = body.Condition.ConditionRevision;

            LocalizedStructuralDamageResult missing = body.Condition.PreviewLocalizedDamage(Request(body, "tx.condition.missing", "injury.blunt-trauma", "part.missing", 10), body.CreateAnatomySnapshot(), body.BiologicalCompatibility, body.CreateSnapshot());
            LocalizedStructuralDamageResult incompatible = body.Condition.PreviewLocalizedDamage(Request(body, "tx.condition.incompatible", "injury.fracture", "core.spiritual", 25), body.CreateAnatomySnapshot(), body.BiologicalCompatibility, body.CreateSnapshot());

            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Code, Is.EqualTo(LocalizedDamageResultCode.MissingAnatomyNode));
            Assert.That(incompatible.Succeeded, Is.False);
            Assert.That(incompatible.Code, Is.EqualTo(LocalizedDamageResultCode.IncompatibleInjury));
            Assert.That(body.Condition.ConditionRevision, Is.EqualTo(revisionBefore));
            Assert.That(body.Condition.CreateSnapshot().ActiveInjuries, Is.Empty);
        }

        [Test]
        public void SaveRestore_PreservesConditionAndEmitsNoConditionEvents()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateBodyRuntime(registry, "actor.runtime.condition.restore", "person.condition");
            Assert.That(original.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(original.Condition.ApplyLocalizedDamage(Request(original, "tx.condition.restore", "injury.fracture", "part.leg.left", 30), original.CreateAnatomySnapshot(), compatibility: original.BiologicalCompatibility, body: original.CreateSnapshot()).Succeeded, Is.True);
            BodySaveData saveData = original.CreateSaveData();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.condition.restore", "person.condition");
            int events = 0;
            restored.Condition.ConditionChanged += (_, _, _) => events++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.condition.restore", "person.condition", restoring: true);
            BodyConditionSnapshot snapshot = restored.Condition.CreateSnapshot();

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(events, Is.EqualTo(0));
            Assert.That(snapshot.ActiveInjuries.Count, Is.EqualTo(1));
            Assert.That(snapshot.ActiveInjuries[0].InjuryDefinitionId, Is.EqualTo("injury.fracture"));
            Assert.That(GetStructure(snapshot, "part.leg.left").CurrentIntegrity, Is.EqualTo(70));
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.condition.restore", "person.condition", out string failure), Is.True, failure);
        }

        [Test]
        public void RuntimeConditionCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/Condition";
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
            GameObject owner = new GameObject("Body Condition Test Body");
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

        private static LocalizedStructuralDamageRequest Request(ActorBodyRuntime body, string transactionId, string injuryDefinitionId, string nodeId, int structuralDamage)
        {
            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            return new LocalizedStructuralDamageRequest
            {
                TransactionId = transactionId,
                SourceActorBodyId = body.ActorBodyId,
                TargetActorBodyId = body.ActorBodyId,
                TargetNodeId = nodeId,
                InjuryDefinitionId = injuryDefinitionId,
                StructuralDamage = structuralDamage,
                ExpectedBodyRevision = anatomy.BodyRevision,
                ExpectedAnatomyRevision = anatomy.AnatomyRevision,
                Context = "Edit Mode body condition test"
            };
        }

        private static StructureConditionSnapshot GetStructure(BodyConditionSnapshot snapshot, string nodeId)
        {
            StructureConditionSnapshot structure = snapshot.Structures.FirstOrDefault(candidate => candidate.NodeId == nodeId);
            Assert.That(structure, Is.Not.Null, $"Missing condition structure '{nodeId}'.");
            return structure;
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
