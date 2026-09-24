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
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class VitalProcessesTests
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
        public void PrototypeCatalog_ResolvesCanonicalBiologicalResourcesAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Blood);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Breath);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Temperature);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Nutrition);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Hydration);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.SleepNeed);
            AssertResolves<BiologicalResourceDefinition>(registry, BiologicalResourceIds.Fatigue);
            AssertResolves<VitalProcessProfileDefinition>(registry, "vital-profile.human");
            AssertResolves<VitalProcessProfileDefinition>(registry, "vital-profile.undead-human");
            AssertResolves<VitalProcessProfileDefinition>(registry, "vital-profile.basic-construct");
            AssertResolves<VitalProcessProfileDefinition>(registry, "vital-profile.basic-spirit");
        }

        [Test]
        public void HumanConstructAndSpiritProfiles_ActivateDifferentResourceSets()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime body = CreateBodyRuntime(registry, "actor.runtime.vital.profile", "person.vital");

            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            VitalProcessSnapshot human = body.VitalProcesses.CreateSnapshot();
            Assert.That(Get(human, BiologicalResourceIds.Blood).Active, Is.True);
            Assert.That(Get(human, BiologicalResourceIds.Breath).Active, Is.True);
            Assert.That(Get(human, BiologicalResourceIds.Nutrition).Active, Is.True);
            Assert.That(Get(human, BiologicalResourceIds.SleepNeed).ModelType, Is.EqualTo(BiologicalResourceModelType.AccumulatingNeed));

            Assert.That(body.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            VitalProcessSnapshot construct = body.VitalProcesses.CreateSnapshot();
            Assert.That(Get(construct, BiologicalResourceIds.Blood).Active, Is.False);
            Assert.That(Get(construct, BiologicalResourceIds.Breath).Active, Is.False);
            Assert.That(Get(construct, BiologicalResourceIds.Temperature).Active, Is.True);

            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            VitalProcessSnapshot spirit = body.VitalProcesses.CreateSnapshot();
            Assert.That(spirit.ActiveResources, Is.Empty);
        }

        [Test]
        public void PreviewMutation_UsesRuntimeRulesWithoutMutationEventsOrRevisionChange()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.vital.preview");
            long revisionBefore = body.VitalProcesses.VitalRevision;
            int events = 0;
            body.VitalProcesses.VitalResourceChanged += (_, _, _) => events++;
            float bloodBefore = Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood).CurrentValue;

            VitalResourceMutationResult result = body.VitalProcesses.PreviewMutation(Request(body, BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 25f, "tx.vital.preview"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(revisionBefore));
            Assert.That(Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood).CurrentValue, Is.EqualTo(bloodBefore));
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void ExecuteMutation_MutatesOnceAndDuplicateTransactionIsIdempotent()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.vital.execute");
            long revisionBefore = body.VitalProcesses.VitalRevision;
            int events = 0;
            body.VitalProcesses.VitalResourceChanged += (_, result, restoring) =>
            {
                Assert.That(restoring, Is.False);
                if (!result.Duplicate)
                {
                    events++;
                }
            };

            VitalResourceMutationRequest request = Request(body, BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 20f, "tx.vital.execute");
            VitalResourceMutationResult first = body.VitalProcesses.ApplyMutation(request, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());
            VitalResourceMutationResult second = body.VitalProcesses.ApplyMutation(request, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.AppliedAmount, Is.EqualTo(20f).Within(0.001f));
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood).CurrentValue, Is.EqualTo(80f).Within(0.001f));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(revisionBefore + 1));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void InactiveResourceRejectsMutationWithoutStateChange()
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), "actor.runtime.vital.inactive", "person.vital");
            Assert.That(body.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            long revisionBefore = body.VitalProcesses.VitalRevision;

            VitalResourceMutationResult result = body.VitalProcesses.ApplyMutation(Request(body, BiologicalResourceIds.Breath, VitalResourceMutationOperation.Consume, 10f, "tx.vital.inactive"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(VitalProcessResultCode.InactiveResource));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void TargetCenteredTemperature_ClassifiesLowHighAndNormal()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.vital.temperature");

            Assert.That(body.VitalProcesses.ApplyMutation(Request(body, BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, 34f, "tx.vital.temp.low"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Temperature).State, Is.EqualTo(VitalProcessState.StrainedLow));

            Assert.That(body.VitalProcesses.ApplyMutation(Request(body, BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, 41f, "tx.vital.temp.high"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Temperature).State, Is.EqualTo(VitalProcessState.CriticalHigh));

            Assert.That(body.VitalProcesses.ApplyMutation(Request(body, BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, 37f, "tx.vital.temp.normal"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Temperature).State, Is.EqualTo(VitalProcessState.Normal));
        }

        [Test]
        public void LungConditionReducesBreathCapacityAndRebuildRestoresIt()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.vital.lung");
            float maxBefore = Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Breath).EffectiveMaximumValue;

            LocalizedStructuralDamageResult damage = body.Condition.ApplyLocalizedDamage(ConditionRequest(body, "tx.vital.lung.damage", "injury.blunt-trauma", "organ.lung.left", 50), body.CreateAnatomySnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            Assert.That(damage.Succeeded, Is.True, damage.Message);
            body.VitalProcesses.RecalculateCapacities(body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), preservingCurrent: true);

            float maxAfterDamage = Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Breath).EffectiveMaximumValue;
            Assert.That(maxAfterDamage, Is.LessThan(maxBefore));

            DefinitionRegistry registry = LoadRegistry();
            LocalizedStructuralDamageResult reset = body.Condition.BuildHealthy(body.ActorBodyId, body.CreateAnatomySnapshot(), registry, restoring: false, preserveRevision: false);
            Assert.That(reset.Succeeded, Is.True, reset.Message);
            body.VitalProcesses.BuildForBody(body.ActorBodyId, body.Species, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), registry);
            float maxAfterReset = Get(body.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Breath).EffectiveMaximumValue;
            Assert.That(maxAfterReset, Is.EqualTo(maxBefore).Within(0.001f));
        }

        [Test]
        public void ProcessUpdate_IsDeterministicAndDoesNotUseWallClock()
        {
            ActorBodyRuntime first = CreateHumanBody("actor.runtime.vital.update.a");
            ActorBodyRuntime second = CreateHumanBody("actor.runtime.vital.update.b");

            VitalResourceMutationResult firstResult = first.VitalProcesses.ApplyProcessUpdate(3600f, "tx.vital.update.a", first.CreateAnatomySnapshot(), first.Condition.CreateSnapshot());
            VitalResourceMutationResult secondResult = second.VitalProcesses.ApplyProcessUpdate(3600f, "tx.vital.update.b", second.CreateAnatomySnapshot(), second.Condition.CreateSnapshot());

            Assert.That(firstResult.Succeeded, Is.True, firstResult.Message);
            Assert.That(secondResult.Succeeded, Is.True, secondResult.Message);
            Assert.That(Get(first.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Nutrition).CurrentValue, Is.EqualTo(Get(second.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Nutrition).CurrentValue).Within(0.001f));
            Assert.That(Get(first.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.SleepNeed).CurrentValue, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void SaveRestorePreservesVitalStateSilentlyAndRejectsWrongBody()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateHumanBody("actor.runtime.vital.restore");
            Assert.That(original.VitalProcesses.ApplyMutation(Request(original, BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, 30f, "tx.vital.restore"), original.CreateAnatomySnapshot(), original.Condition.CreateSnapshot()).Succeeded, Is.True);
            BodySaveData saveData = original.CreateSaveData();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.vital.restore", "person.vital");
            int events = 0;
            restored.VitalProcesses.VitalResourceChanged += (_, _, _) => events++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.vital.restore", "person.vital", restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(events, Is.EqualTo(0));
            Assert.That(Get(restored.VitalProcesses.CreateSnapshot(), BiologicalResourceIds.Blood).CurrentValue, Is.EqualTo(70f).Within(0.001f));
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.vital.other", "person.vital", out string failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("does not match"));
        }

        [Test]
        public void RuntimeVitalProcessCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/VitalProcesses";
            foreach (string file in Directory.GetFiles(runtimeFolder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.Development"), file);
                Assert.That(text, Does.Not.Contain("UnityIsekaiGame.UI"), file);
                Assert.That(text, Does.Not.Contain("UnityEditor"), file);
                Assert.That(text, Does.Not.Contain("Prototype"), file);
            }
        }

        private ActorBodyRuntime CreateHumanBody(string actorBodyId)
        {
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), actorBodyId, "person.vital");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            return body;
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Vital Process Test Body");
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

        private static VitalResourceMutationRequest Request(ActorBodyRuntime body, string resourceId, VitalResourceMutationOperation operation, float amount, string transactionId)
        {
            AnatomySnapshot anatomy = body.CreateAnatomySnapshot();
            BodyConditionSnapshot condition = body.Condition.CreateSnapshot();
            return new VitalResourceMutationRequest(
                body.ActorBodyId,
                resourceId,
                operation,
                amount,
                transactionId,
                "edit-mode-test",
                "Edit Mode vital process test",
                body.BodyRevision,
                anatomy.AnatomyRevision,
                condition.ConditionRevision);
        }

        private static LocalizedStructuralDamageRequest ConditionRequest(ActorBodyRuntime body, string transactionId, string injuryDefinitionId, string nodeId, int structuralDamage)
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
                Context = "Edit Mode vital process test"
            };
        }

        private static VitalResourceSnapshot Get(VitalProcessSnapshot snapshot, string resourceId)
        {
            VitalResourceSnapshot resource = snapshot.Resources.FirstOrDefault(candidate => candidate.ResourceId == resourceId);
            Assert.That(resource, Is.Not.Null, $"Missing vital resource '{resourceId}'.");
            return resource;
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
