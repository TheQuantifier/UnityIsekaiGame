using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Tests
{
    public sealed class BiologicalCompatibilityTests
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
        public void PrototypeCatalog_ResolvesCanonicalBiologicalCompatibilityAndValidates()
        {
            DefinitionCatalog catalog = LoadCatalog();
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.EqualTo(0), report.GetSummary());

            DefinitionRegistry registry = catalog.CreateRegistry(report);
            foreach (string interactionId in BiologicalInteractionCanonicalSet.RequiredInteractionIds)
            {
                AssertResolves<BiologicalInteractionDefinition>(registry, interactionId);
            }

            AssertResolves<BiologicalCompatibilityProfileDefinition>(registry, "compatibility-profile.species.human");
            AssertResolves<BiologicalCompatibilityProfileDefinition>(registry, "compatibility-profile.species.undead-human");
            AssertResolves<BiologicalCompatibilityProfileDefinition>(registry, "compatibility-profile.species.basic-construct");
            AssertResolves<BiologicalCompatibilityProfileDefinition>(registry, "compatibility-profile.species.basic-spirit");
        }

        [Test]
        public void SpeciesProfiles_ProduceExpectedHumanUndeadConstructAndSpiritRules()
        {
            DefinitionRegistry registry = LoadRegistry();

            ActorBodyRuntime human = CreateBodyRuntime(registry, "actor.runtime.compatibility.human", "person.compatibility");
            Assert.That(human.AssignSpecies("species.human").Succeeded, Is.True);
            Assert.That(Evaluate(human, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).Compatible, Is.True);
            Assert.That(Evaluate(human, BiologicalInteractionIds.Suffocation, BiologicalInteractionCategory.Hazard).Compatible, Is.True);
            Assert.That(Evaluate(human, BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Injury, "part.leg.left").Compatible, Is.True);

            ActorBodyRuntime undead = CreateBodyRuntime(registry, "actor.runtime.compatibility.undead", "person.compatibility");
            Assert.That(undead.AssignSpecies("species.undead-human").Succeeded, Is.True);
            BiologicalInteractionEvaluationResult undeadDisease = Evaluate(undead, BiologicalInteractionIds.Disease, BiologicalInteractionCategory.Disease);
            BiologicalInteractionEvaluationResult undeadPoison = Evaluate(undead, BiologicalInteractionIds.Poison, BiologicalInteractionCategory.Poison);
            Assert.That(undeadDisease.Immune, Is.True);
            Assert.That(undeadPoison.RateMultiplier, Is.LessThan(1f));

            ActorBodyRuntime construct = CreateBodyRuntime(registry, "actor.runtime.compatibility.construct", "person.compatibility");
            Assert.That(construct.AssignSpecies("species.basic-construct").Succeeded, Is.True);
            Assert.That(Evaluate(construct, BiologicalInteractionIds.ConstructRepair, BiologicalInteractionCategory.Repair).Compatible, Is.True);
            Assert.That(Evaluate(construct, BiologicalInteractionIds.BiologicalHealing, BiologicalInteractionCategory.Healing).Compatible, Is.False);
            Assert.That(Evaluate(construct, BiologicalInteractionIds.Suffocation, BiologicalInteractionCategory.Hazard).Compatible, Is.False);
            Assert.That(Evaluate(construct, BiologicalInteractionIds.CoreDamage, BiologicalInteractionCategory.Injury, "core.power").Compatible, Is.True);

            ActorBodyRuntime spirit = CreateBodyRuntime(registry, "actor.runtime.compatibility.spirit", "person.compatibility");
            Assert.That(spirit.AssignSpecies("species.basic-spirit").Succeeded, Is.True);
            Assert.That(Evaluate(spirit, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).Compatible, Is.False);
            Assert.That(Evaluate(spirit, BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Injury, "essence.aura").Compatible, Is.False);
            Assert.That(Evaluate(spirit, BiologicalInteractionIds.IncorporealDisruption, BiologicalInteractionCategory.Injury, "essence.aura").Compatible, Is.True);
        }

        [Test]
        public void RuleContributions_AreSourceOwnedAndRemovalPreservesOtherSources()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.source-owned");

            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.a", "source.a", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f)).Succeeded, Is.True);
            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.b", "source.b", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f)).Succeeded, Is.True);
            Assert.That(Evaluate(body, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).RateMultiplier, Is.EqualTo(0.25f).Within(0.001f));

            BiologicalCompatibilityOperationResult remove = body.BiologicalCompatibility.RemoveContribution("source.a", "entry.a");

            Assert.That(remove.Succeeded, Is.True, remove.Message);
            BiologicalInteractionEvaluationResult result = Evaluate(body, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard);
            Assert.That(result.RateMultiplier, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(body.BiologicalCompatibility.CreateSnapshot().Rules.Select(rule => rule.EntryId), Is.EquivalentTo(new[] { "entry.b" }));
        }

        [Test]
        public void EvaluationOrder_IsDeterministicAcrossContributionApplicationOrder()
        {
            ActorBodyRuntime first = CreateHumanBody("actor.runtime.compatibility.order-a");
            ActorBodyRuntime second = CreateHumanBody("actor.runtime.compatibility.order-b");

            first.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.b", "source.b", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.8f));
            first.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.a", "source.a", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f));
            second.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.a", "source.a", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f));
            second.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.b", "source.b", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.8f));

            BiologicalInteractionEvaluationResult firstResult = Evaluate(first, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard);
            BiologicalInteractionEvaluationResult secondResult = Evaluate(second, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard);

            Assert.That(firstResult.RateMultiplier, Is.EqualTo(secondResult.RateMultiplier).Within(0.001f));
            Assert.That(MatchedDynamicEntries(firstResult), Is.EqualTo(MatchedDynamicEntries(secondResult)));
        }

        [Test]
        public void CategoryRulesApplyBeforeSpecificRulesAtEqualPriority()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.precedence");
            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.category", "source.category", string.Empty, BiologicalInteractionCategory.Injury, BiologicalInteractionRuleKind.CompatibilityOverride, BiologicalCompatibilityState.Incompatible, 1f, 10)).Succeeded, Is.True);
            Assert.That(body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.specific", "source.specific", BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Unknown, BiologicalInteractionRuleKind.CompatibilityOverride, BiologicalCompatibilityState.Compatible, 1f, 10)).Succeeded, Is.True);

            BiologicalInteractionEvaluationResult fracture = Evaluate(body, BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Injury, "part.leg.left");
            BiologicalInteractionEvaluationResult laceration = Evaluate(body, BiologicalInteractionIds.Laceration, BiologicalInteractionCategory.Injury, "part.hand.left");

            Assert.That(fracture.Compatible, Is.True, fracture.Message);
            Assert.That(laceration.Compatible, Is.False, laceration.Message);
        }

        [Test]
        public void DynamicRulesAreSourceClearedAndUpdatesAreNotMisclassifiedAsDuplicates()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.dynamic-reset");
            BiologicalCompatibilityOperationResult initial = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.bleed", "source.dynamic", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Immunity, 1f));
            Assert.That(initial.Succeeded, Is.True, initial.Message);
            Assert.That(Evaluate(body, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).Immune, Is.True);

            BiologicalCompatibilityOperationResult cleared = body.BiologicalCompatibility.ClearSource("source.dynamic");
            Assert.That(cleared.Succeeded, Is.True, cleared.Message);
            Assert.That(Evaluate(body, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).Compatible, Is.True);

            BiologicalCompatibilityOperationResult first = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.bleed", "source.dynamic", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f));
            BiologicalCompatibilityOperationResult changed = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.bleed", "source.dynamic", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.8f));
            BiologicalCompatibilityOperationResult duplicate = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.bleed", "source.dynamic", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.8f));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(changed.Succeeded, Is.True, changed.Message);
            Assert.That(changed.Duplicate, Is.False);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(Evaluate(body, BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard).RateMultiplier, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void HazardRuntime_ConsumesCompatibilityWithoutMutatingWhenRejected()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.hazard");
            body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.immune", "source.compatibility", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Immunity, 1f));
            long revisionBefore = body.BiologicalHazards.HazardRevision;

            BiologicalHazardOperationResult result = body.BiologicalHazards.AddOrUpdateSource(
                new BiologicalHazardSourceRequest(body.ActorBodyId, BiologicalHazardIds.Bleeding, "source.bleeding", BiologicalHazardSourceCategory.Injury, BiologicalHazardSeverity.Moderate),
                body.VitalProcesses,
                body.CreateAnatomySnapshot(),
                body.Condition.CreateSnapshot(),
                compatibility: body.BiologicalCompatibility,
                body: body.CreateSnapshot());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(BiologicalHazardResultCode.InvalidRequest));
            Assert.That(body.BiologicalHazards.HazardRevision, Is.EqualTo(revisionBefore));
            Assert.That(body.BiologicalHazards.CreateSnapshot().ActiveHazards, Is.Empty);
        }

        [Test]
        public void ConditionAndHazardExecutionFailClosedWithoutCurrentCompatibilityContext()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.fail-closed");
            ActorBodyRuntime other = CreateHumanBody("actor.runtime.compatibility.other-body");
            LocalizedStructuralDamageRequest request = Request(body, "tx.compatibility.missing-context", "injury.fracture", "part.leg.left", 10);

            LocalizedStructuralDamageResult missing = body.Condition.PreviewLocalizedDamage(request, body.CreateAnatomySnapshot(), null, body.CreateSnapshot());
            LocalizedStructuralDamageResult stale = body.Condition.PreviewLocalizedDamage(request, body.CreateAnatomySnapshot(), other.BiologicalCompatibility, other.CreateSnapshot());
            BiologicalHazardOperationResult hazardMissing = body.BiologicalHazards.AddOrUpdateSource(
                new BiologicalHazardSourceRequest(body.ActorBodyId, BiologicalHazardIds.Bleeding, "source.missing-context", BiologicalHazardSourceCategory.Injury, BiologicalHazardSeverity.Moderate),
                body.VitalProcesses,
                body.CreateAnatomySnapshot(),
                body.Condition.CreateSnapshot(),
                compatibility: null,
                body: body.CreateSnapshot());

            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Code, Is.EqualTo(LocalizedDamageResultCode.MissingCompatibility));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Code, Is.EqualTo(LocalizedDamageResultCode.StaleBody));
            Assert.That(hazardMissing.Succeeded, Is.False);
            Assert.That(hazardMissing.Code, Is.EqualTo(BiologicalHazardResultCode.MissingCompatibility));
        }

        [Test]
        public void StaleBodySnapshotsAndMissingInteractionsRejectClearly()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.stale");
            BodySnapshot oldSnapshot = body.CreateSnapshot();
            Assert.That(body.AssignSpecies("species.basic-construct").Succeeded, Is.True);

            BiologicalInteractionEvaluationResult stale = body.BiologicalCompatibility.Evaluate(oldSnapshot, BiologicalInteractionIds.CoreDamage, BiologicalInteractionCategory.Injury, preview: true);
            BiologicalInteractionEvaluationResult missing = body.BiologicalCompatibility.Evaluate(body.CreateSnapshot(), "interaction.missing.not-authored", BiologicalInteractionCategory.Hazard, preview: true);
            BiologicalCompatibilityOperationResult missingContribution = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.missing", "source.missing", "interaction.missing.not-authored", BiologicalInteractionRuleKind.Resistance, 0.5f));
            BiologicalCompatibilityOperationResult missingConversion = body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.bad-conversion", "source.missing", BiologicalInteractionIds.Necrotic, BiologicalInteractionCategory.Unknown, BiologicalInteractionRuleKind.Conversion, BiologicalCompatibilityState.Compatible, 1f, 10, "interaction.missing.converted"));

            Assert.That(stale.Code, Is.EqualTo(BiologicalCompatibilityResultCode.StaleBody), stale.Message);
            Assert.That(missing.Code, Is.EqualTo(BiologicalCompatibilityResultCode.MissingInteraction), missing.Message);
            Assert.That(missingContribution.Succeeded, Is.False);
            Assert.That(missingContribution.Code, Is.EqualTo(BiologicalCompatibilityResultCode.MissingInteraction), missingContribution.Message);
            Assert.That(missingConversion.Succeeded, Is.False);
            Assert.That(missingConversion.Code, Is.EqualTo(BiologicalCompatibilityResultCode.MissingInteraction), missingConversion.Message);
        }

        [Test]
        public void InjuryRuntime_ConsumesCompatibilityAndAppliesAllowedCompatibilityMultiplier()
        {
            ActorBodyRuntime blocked = CreateHumanBody("actor.runtime.compatibility.injury-blocked");
            blocked.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.suppress", "source.compatibility", BiologicalInteractionIds.Fracture, BiologicalInteractionRuleKind.Suppression, 0f));

            LocalizedStructuralDamageResult rejected = blocked.Condition.ApplyLocalizedDamage(
                Request(blocked, "tx.compatibility.fracture.rejected", "injury.fracture", "part.leg.left", 30),
                blocked.CreateAnatomySnapshot(),
                compatibility: blocked.BiologicalCompatibility,
                body: blocked.CreateSnapshot());

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(LocalizedDamageResultCode.IncompatibleInjury));
            Assert.That(GetStructure(blocked.Condition.CreateSnapshot(), "part.leg.left").CurrentIntegrity, Is.EqualTo(100));

            ActorBodyRuntime resisted = CreateHumanBody("actor.runtime.compatibility.injury-resisted");
            resisted.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.resist", "source.compatibility", BiologicalInteractionIds.Fracture, BiologicalInteractionRuleKind.Resistance, 0.5f));
            LocalizedStructuralDamageResult applied = resisted.Condition.ApplyLocalizedDamage(
                Request(resisted, "tx.compatibility.fracture.resisted", "injury.fracture", "part.leg.left", 30),
                resisted.CreateAnatomySnapshot(),
                compatibility: resisted.BiologicalCompatibility,
                body: resisted.CreateSnapshot());

            Assert.That(applied.Succeeded, Is.True, applied.Message);
            Assert.That(applied.DamageApplied, Is.EqualTo(15));
            Assert.That(GetStructure(resisted.Condition.CreateSnapshot(), "part.leg.left").CurrentIntegrity, Is.EqualTo(85));
        }

        [Test]
        public void Snapshots_AreReadOnlyAndCoherentWithoutDirtyingRuntime()
        {
            ActorBodyRuntime body = CreateHumanBody("actor.runtime.compatibility.snapshot");
            body.BiologicalCompatibility.AddOrUpdateContribution(Rule("entry.a", "source.a", BiologicalInteractionIds.Bleeding, BiologicalInteractionRuleKind.Resistance, 0.5f));
            long revisionBefore = body.BiologicalCompatibility.CompatibilityRevision;

            BiologicalCompatibilitySnapshot snapshot = body.BiologicalCompatibility.CreateSnapshot();

            Assert.That(snapshot.Coherent, Is.True, string.Join(Environment.NewLine, snapshot.Diagnostics));
            Assert.That(body.BiologicalCompatibility.CompatibilityRevision, Is.EqualTo(revisionBefore));
            Assert.That(() => ((IList<BiologicalCompatibilityRuleSnapshot>)snapshot.Rules).Clear(), Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void InvalidRuntimeRuleNumbersAreSanitizedWithoutCollapsingSeverityCeilings()
        {
            RuntimeBiologicalInteractionRule rule = new RuntimeBiologicalInteractionRule(
                "entry.invalid-numbers",
                BiologicalCompatibilitySourceKind.System,
                "source.invalid-numbers",
                BiologicalInteractionIds.Bleeding,
                BiologicalInteractionCategory.Unknown,
                BiologicalInteractionRuleKind.Resistance,
                BiologicalCompatibilityState.Compatible,
                float.NaN,
                float.PositiveInfinity,
                -1f,
                float.NegativeInfinity,
                float.PositiveInfinity,
                0,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Invalid number sanitization test");

            Assert.That(rule.RateMultiplier, Is.EqualTo(1f));
            Assert.That(rule.SeverityMultiplier, Is.EqualTo(1f));
            Assert.That(rule.ConsequenceMultiplier, Is.EqualTo(1f));
            Assert.That(rule.MinimumEffectFloor, Is.EqualTo(0f));
            Assert.That(rule.MaximumSeverity, Is.EqualTo(999f));
        }

        [Test]
        public void RuntimeCompatibilityCode_HasNoDevelopmentPrototypeUiOrEditorDependency()
        {
            string runtimeFolder = "Assets/_Project/Runtime/Actors/Beings/Biology/Compatibility";
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
            ActorBodyRuntime body = CreateBodyRuntime(LoadRegistry(), actorBodyId, "person.compatibility");
            Assert.That(body.AssignSpecies("species.human").Succeeded, Is.True);
            return body;
        }

        private ActorBodyRuntime CreateBodyRuntime(DefinitionRegistry registry, string actorBodyId, string personId)
        {
            GameObject owner = new GameObject("Biological Compatibility Test Body");
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

        private static RuntimeBiologicalInteractionRule Rule(string entryId, string sourceId, string interactionId, BiologicalInteractionRuleKind kind, float multiplier)
        {
            return Rule(entryId, sourceId, interactionId, BiologicalInteractionCategory.Unknown, kind, BiologicalCompatibilityState.Compatible, multiplier, 10);
        }

        private static RuntimeBiologicalInteractionRule Rule(string entryId, string sourceId, string interactionId, BiologicalInteractionCategory category, BiologicalInteractionRuleKind kind, BiologicalCompatibilityState compatibilityState, float multiplier, int priority, string convertedInteractionId = "")
        {
            return new RuntimeBiologicalInteractionRule(
                entryId,
                BiologicalCompatibilitySourceKind.System,
                sourceId,
                interactionId,
                category,
                kind,
                compatibilityState,
                multiplier,
                multiplier,
                multiplier,
                0f,
                999f,
                priority,
                convertedInteractionId,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AnatomyStructuralCategory>(),
                string.Empty,
                "Edit Mode biological compatibility test");
        }

        private static BiologicalInteractionEvaluationResult Evaluate(ActorBodyRuntime body, string interactionId, BiologicalInteractionCategory category, string targetNodeId = "")
        {
            AnatomyNodeSnapshot targetNode = string.IsNullOrWhiteSpace(targetNodeId)
                ? null
                : body.CreateAnatomySnapshot().Nodes.First(node => string.Equals(node.NodeId, targetNodeId, StringComparison.Ordinal));
            return body.BiologicalCompatibility.Evaluate(body.CreateSnapshot(), interactionId, category, targetNode, "edit-mode-test", $"tx.compatibility.evaluate.{Guid.NewGuid():N}", preview: true);
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
                Context = "Edit Mode biological compatibility test"
            };
        }

        private static string[] MatchedDynamicEntries(BiologicalInteractionEvaluationResult result)
        {
            return result.RuleTrace
                .Where(trace => trace.Matched && trace.SourceId.StartsWith("source.", StringComparison.Ordinal))
                .Select(trace => trace.EntryId)
                .ToArray();
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
