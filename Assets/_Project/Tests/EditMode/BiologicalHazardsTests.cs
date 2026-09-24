using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BiologicalHazardsTests
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
        public void PrototypeCatalog_ResolvesCanonicalBiologicalHazardsAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Bleeding);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Suffocation);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Overheating);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Hypothermia);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Starvation);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.Dehydration);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.ExtremeFatigue);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.SleepDeprivation);
            AssertResolves<BiologicalHazardDefinition>(registry, BiologicalHazardIds.EnvironmentalExposure);
            AssertResolves<EnvironmentalExposureDefinition>(registry, BiologicalExposureIds.BreathableAirUnavailable);
            AssertResolves<EnvironmentalExposureDefinition>(registry, BiologicalExposureIds.Heat);
            AssertResolves<EnvironmentalExposureDefinition>(registry, BiologicalExposureIds.Cold);
        }

        [Test]
        public void PreviewTick_UsesVitalRulesWithoutMutationEventsOrRevisionChange()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.hazard.preview");
            AddBleeding(body, "tx.hazard.preview.source");
            long hazardRevision = body.BiologicalHazards.HazardRevision;
            long vitalRevision = body.VitalProcesses.VitalRevision;
            float bloodBefore = GetVital(body, BiologicalResourceIds.Blood).CurrentValue;
            int hazardEvents = 0;
            int vitalEvents = 0;
            body.BiologicalHazards.HazardTicked += (_, _, _) => hazardEvents++;
            body.VitalProcesses.VitalResourceChanged += (_, _, _) => vitalEvents++;

            BiologicalHazardTickResult result = body.BiologicalHazards.PreviewTick(Tick(body, "tx.hazard.preview", 1800f, preview: true), body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), body.BiologicalCompatibility, body.CreateSnapshot());

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(body.BiologicalHazards.HazardRevision, Is.EqualTo(hazardRevision));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(vitalRevision));
            Assert.That(GetVital(body, BiologicalResourceIds.Blood).CurrentValue, Is.EqualTo(bloodBefore).Within(0.001f));
            Assert.That(hazardEvents, Is.EqualTo(0));
            Assert.That(vitalEvents, Is.EqualTo(0));
        }

        [Test]
        public void ExecuteTick_ConsumesVitalResourceOnceAndDuplicateIsIdempotent()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.hazard.execute");
            AddBleeding(body, "tx.hazard.execute.source");
            long hazardRevision = body.BiologicalHazards.HazardRevision;
            long vitalRevision = body.VitalProcesses.VitalRevision;

            BiologicalHazardTickRequest request = Tick(body, "tx.hazard.execute", 1800f);
            BiologicalHazardTickResult first = body.BiologicalHazards.ApplyTick(request, body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
            BiologicalHazardTickResult second = body.BiologicalHazards.ApplyTick(request, body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(GetVital(body, BiologicalResourceIds.Blood).CurrentValue, Is.EqualTo(90f).Within(0.001f));
            Assert.That(body.BiologicalHazards.HazardRevision, Is.EqualTo(hazardRevision + 1));
            Assert.That(body.VitalProcesses.VitalRevision, Is.EqualTo(vitalRevision + 1));
        }

        [Test]
        public void SourceRemoval_RemovesOnlyExactSourceAndKeepsOtherSources()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.hazard.sources");
            AddBleeding(body, "source.bleed.a");
            AddBleeding(body, "source.bleed.b", 0.5f);

            BiologicalHazardOperationResult remove = body.BiologicalHazards.RemoveSource(BiologicalHazardIds.Bleeding, "source.bleed.a");

            Assert.That(remove.Succeeded, Is.True, remove.Message);
            BiologicalHazardInstanceSnapshot bleeding = GetHazard(body, BiologicalHazardIds.Bleeding);
            Assert.That(bleeding.Sources.Select(source => source.SourceContributionId), Is.EquivalentTo(new[] { "source.bleed.b" }));
        }

        [Test]
        public void InactiveBloodAndBreathRejectHazardsWithoutMutation()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime construct = CreateBodyRuntime(registry, "actor.runtime.hazard.construct", "person.hazard");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            BiologicalHazardOperationResult bleeding = AddBleeding(construct, "source.construct.bleeding");
            Assert.That(bleeding.Succeeded, Is.False);
            Assert.That(bleeding.Code, Is.EqualTo(BiologicalHazardResultCode.InactiveResource));

            ActorBodyRuntime spirit = CreateBodyRuntime(registry, "actor.runtime.hazard.spirit", "person.hazard");
            Assert.That(spirit.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            BiologicalHazardOperationResult suffocation = AddSource(spirit, BiologicalHazardIds.Suffocation, "source.spirit.suffocation", BiologicalHazardSourceCategory.Environment, BiologicalHazardSeverity.Serious);
            Assert.That(suffocation.Succeeded, Is.False);
            Assert.That(suffocation.Code, Is.EqualTo(BiologicalHazardResultCode.InactiveResource));
        }

        [Test]
        public void CriticalVitalsCreateSurvivalHazardsAndTemperatureHazardsAreExclusive()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.hazard.sync");
            ApplyVital(body, BiologicalResourceIds.Nutrition, VitalResourceMutationOperation.Set, 0f, "tx.hazard.nutrition");
            ApplyVital(body, BiologicalResourceIds.Hydration, VitalResourceMutationOperation.Set, 0f, "tx.hazard.hydration");
            Assert.That(body.BiologicalHazards.SynchronizeFromVitalProcesses(body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(HasHazard(body, BiologicalHazardIds.Starvation), Is.True);
            Assert.That(HasHazard(body, BiologicalHazardIds.Dehydration), Is.True);

            ApplyVital(body, BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, 42f, "tx.hazard.temp.high");
            Assert.That(body.BiologicalHazards.SynchronizeFromVitalProcesses(body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(HasHazard(body, BiologicalHazardIds.Overheating), Is.True);
            Assert.That(HasHazard(body, BiologicalHazardIds.Hypothermia), Is.False);

            ApplyVital(body, BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, 30f, "tx.hazard.temp.low");
            Assert.That(body.BiologicalHazards.SynchronizeFromVitalProcesses(body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot()).Succeeded, Is.True);
            Assert.That(HasHazard(body, BiologicalHazardIds.Overheating), Is.False);
            Assert.That(HasHazard(body, BiologicalHazardIds.Hypothermia), Is.True);
        }

        [Test]
        public void SaveRestorePreservesHazardsSilentlyAndRejectsWrongBody()
        {
            DefinitionRegistry registry = LoadRegistry();
            ActorBodyRuntime original = CreateHumanBody("actor.runtime.hazard.restore");
            AddBleeding(original, "source.restore.bleeding");
            BodySaveData saveData = original.CreateSaveData();

            ActorBodyRuntime restored = CreateBodyRuntime(registry, "actor.runtime.hazard.restore", "person.hazard");
            int events = 0;
            restored.BiologicalHazards.HazardChanged += (_, _, _) => events++;
            restored.BiologicalHazards.HazardTicked += (_, _, _) => events++;
            BodyOperationResult restore = restored.RestoreFromSaveData(saveData, registry, "actor.runtime.hazard.restore", "person.hazard", restoring: true);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(events, Is.EqualTo(0));
            Assert.That(HasHazard(restored, BiologicalHazardIds.Bleeding), Is.True);
            Assert.That(ActorBodyRuntime.ValidateSaveData(saveData, registry, "actor.runtime.hazard.other", "person.hazard", out string failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("does not match"));
        }

        [Test]
        public void RuntimeHazardCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/Hazards";
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
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), actorBodyId, "person.hazard");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            return body;
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Biological Hazard Test Body");
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

        private static BiologicalHazardOperationResult AddBleeding(ActorBodyRuntime body, string sourceId, float rate = 1f)
        {
            return AddSource(body, BiologicalHazardIds.Bleeding, sourceId, BiologicalHazardSourceCategory.Injury, BiologicalHazardSeverity.Moderate, rate);
        }

        private static BiologicalHazardOperationResult AddSource(ActorBodyRuntime body, string hazardId, string sourceId, BiologicalHazardSourceCategory category, BiologicalHazardSeverity severity, float rate = 1f)
        {
            return body.BiologicalHazards.AddOrUpdateSource(new BiologicalHazardSourceRequest(body.ActorBodyId, hazardId, sourceId, category, severity, rate, 0f, "edit-mode-test", "Edit Mode biological hazard test"), body.VitalProcesses, body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot(), compatibility: body.BiologicalCompatibility, body: body.CreateSnapshot());
        }

        private static BiologicalHazardTickRequest Tick(ActorBodyRuntime body, string transactionId, float elapsedSeconds, bool preview = false)
        {
            return new BiologicalHazardTickRequest(body.ActorBodyId, elapsedSeconds, transactionId, preview, "Edit Mode biological hazard test");
        }

        private static void ApplyVital(ActorBodyRuntime body, string resourceId, VitalResourceMutationOperation operation, float amount, string transactionId)
        {
            VitalResourceMutationResult result = body.VitalProcesses.ApplyMutation(new VitalResourceMutationRequest(body.ActorBodyId, resourceId, operation, amount, transactionId, "edit-mode-test", "Edit Mode biological hazard test"), body.CreateAnatomySnapshot(), body.Condition.CreateSnapshot());
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static VitalResourceSnapshot GetVital(ActorBodyRuntime body, string resourceId)
        {
            Assert.That(body.VitalProcesses.TryGetResource(resourceId, out VitalResourceSnapshot resource), Is.True, $"Missing vital resource '{resourceId}'.");
            return resource;
        }

        private static bool HasHazard(ActorBodyRuntime body, string hazardId)
        {
            return body.BiologicalHazards.CreateSnapshot().ActiveHazards.Any(hazard => hazard.HazardDefinitionId == hazardId);
        }

        private static BiologicalHazardInstanceSnapshot GetHazard(ActorBodyRuntime body, string hazardId)
        {
            BiologicalHazardInstanceSnapshot hazard = body.BiologicalHazards.CreateSnapshot().ActiveHazards.FirstOrDefault(candidate => candidate.HazardDefinitionId == hazardId);
            Assert.That(hazard, Is.Not.Null, $"Missing biological hazard '{hazardId}'.");
            return hazard;
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
