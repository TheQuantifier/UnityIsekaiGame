using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Tests
{
    public sealed class DisassemblyRuntimeTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = "person.disassembly.test";

        [Test]
        public void PreviewIsDeterministicAndDoesNotMutateRuntime()
        {
            Fixture fixture = CreateFixture();
            DisassemblyRequest request = Request(fixture.ItemId, "preview.operation", ItemRecoveryOperationKind.Disassemble, 3, true);

            DisassemblyResult first = fixture.Runtime.Preview(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);
            DisassemblyResult second = fixture.Runtime.Preview(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Preview, Is.True);
            Assert.That(fixture.Runtime.Operations, Is.Empty);
            Assert.That(first.Operation.actualEfficiency, Is.EqualTo(second.Operation.actualEfficiency));
            Assert.That(first.Operation.outcomes[0].recoveryRoll, Is.EqualTo(second.Operation.outcomes[0].recoveryRoll));
            Assert.That(fixture.Items.TryGetSnapshot(fixture.ItemId, out ItemInstanceSnapshot source), Is.True);
            Assert.That(source.LifecycleState, Is.EqualTo(ItemLifecycleState.InInventory));
        }

        [Test]
        public void SkillAndItemTierImproveRecoveryWhileComponentRarityPenalizesIt()
        {
            float unskilled = DisassemblyScalingCalculator.ExpectedEfficiency(0, false, 1, 0, 0.25f, 1f);
            float master = DisassemblyScalingCalculator.ExpectedEfficiency(7, true, 80, 4, 0.9f, 1f);
            float commonChance = DisassemblyScalingCalculator.ComponentRecoveryChance(master, 0, 1f);
            float legendaryChance = DisassemblyScalingCalculator.ComponentRecoveryChance(master, 5, 1f);

            Assert.That(master, Is.GreaterThan(unskilled));
            Assert.That(commonChance, Is.GreaterThan(legendaryChance));
            Assert.That(DisassemblyScalingCalculator.Tier(master), Is.GreaterThan(DisassemblyScalingCalculator.Tier(unskilled)));
        }

        [Test]
        public void ComponentDamageCapsRecoveredQuantity()
        {
            Fixture fixture = CreateFixture();
            DisassemblyRequest request = Request(fixture.ItemId, "condition.full", ItemRecoveryOperationKind.Disassemble, 7, true);
            request.itemLevel = 100;
            request.deterministicSeed = "condition.constant";
            DisassemblyResult full = fixture.Runtime.Preview(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            fixture.Durability.ApplyDamage(fixture.Items, fixture.Compositions, fixture.Quality, fixture.Registry, fixture.ItemId, 55f, ItemDamageChannel.Cutting, "component.blade", "damage.test");
            request.operationId = "condition.damaged";
            DisassemblyResult damaged = fixture.Runtime.Preview(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            DisassemblyComponentOutcomeData fullBlade = full.Operation.outcomes.Find(entry => entry.componentEntryId == "component.blade");
            DisassemblyComponentOutcomeData damagedBlade = damaged.Operation.outcomes.Find(entry => entry.componentEntryId == "component.blade");
            Assert.That(damagedBlade.componentCondition, Is.LessThan(fullBlade.componentCondition));
            Assert.That(damagedBlade.quantityEfficiency, Is.LessThan(fullBlade.quantityEfficiency));
            Assert.That(damagedBlade.recoveryChance, Is.LessThan(fullBlade.recoveryChance));
        }

        [Test]
        public void ExecuteReturnsRecordedComponentsAndIsIdempotentAndPersistent()
        {
            Fixture fixture = CreateFixture();
            DisassemblyRequest request = Request(fixture.ItemId, "execute.operation", ItemRecoveryOperationKind.Disassemble, 7, true);
            request.itemLevel = 100;
            DisassemblyResult result = fixture.Runtime.Execute(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);
            DisassemblyResult duplicate = fixture.Runtime.Execute(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Operation.outcomes.Any(entry => entry.returnedQuantity > 0), Is.True);
            Assert.That(fixture.Items.TryGetSnapshot(fixture.ItemId, out ItemInstanceSnapshot source), Is.True);
            Assert.That(source.LifecycleState, Is.EqualTo(ItemLifecycleState.Disassembled));
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);

            DisassemblyRuntime restored = new DisassemblyRuntime();
            DisassemblyResult restore = restored.RestoreFromSaveData(fixture.Runtime.CreateSaveData(), fixture.Registry);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetOperation(request.operationId, out DisassemblyOperationRecordData restoredOperation), Is.True);
            Assert.That(restoredOperation.outcomes.Count, Is.EqualTo(result.Operation.outcomes.Count));
        }

        [Test]
        public void SalvagePickupUsesSeparateSkillAndSharedRecoveryHistory()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Registry.TryGet("item.prototype-iron-ore", out ItemDefinition resource), Is.True);
            SalvagePickupCalculation calculation = SalvagePickupScalingCalculator.Calculate(4, 7, true, "master.salvage", 1f, 2);
            DisassemblyResult result = fixture.Runtime.RecordSalvagePickup("salvage.pickup.operation", "world-drop.iron", PersonId, "12", resource, calculation, calculation.BaseQuantity, calculation.TotalQuantity);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Operation.operationKind, Is.EqualTo(ItemRecoveryOperationKind.Salvage));
            Assert.That(result.Operation.skillId, Is.EqualTo("skill.salvaging"));
            Assert.That(result.Operation.outcomes[0].quantityMultiplier, Is.EqualTo(2));
            Assert.That(result.Operation.outcomes[0].quantityEfficiency, Is.EqualTo(1f));
            Assert.That(result.Operation.outcomes[0].sourceQuantity, Is.EqualTo(4f));
            Assert.That(result.Operation.outcomes[0].returnedQuantity, Is.EqualTo(8));
        }

        [Test]
        public void MasterSalvagerHasFiftyPercentDoubleYieldChance()
        {
            SalvagePickupCalculation master = SalvagePickupScalingCalculator.Calculate(3, 7, true, "chance.probe");
            SalvagePickupCalculation unskilled = SalvagePickupScalingCalculator.Calculate(3, 0, false, "chance.probe");
            Assert.That(master.BonusChance, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(master.QuantityMultiplier, Is.EqualTo(1).Or.EqualTo(2));
            Assert.That(unskilled.BonusChance, Is.Zero);
            Assert.That(unskilled.TotalQuantity, Is.EqualTo(3));
        }

        [Test]
        public void DisassemblerAndSalvagerAreDistinctProfessionsAndActivities()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            DefinitionRegistry registry = PrototypeProfessionDefinitionFactory.AddMissingPrototypeProfessionDefinitions(catalog.CreateRegistry());

            Assert.That(registry.TryGet(PrototypeProfessionDefinitionFactory.DisassemblerProfessionId, out ProfessionDefinition disassembler), Is.True);
            Assert.That(registry.TryGet(PrototypeProfessionDefinitionFactory.SalvagerProfessionId, out ProfessionDefinition salvager), Is.True);
            Assert.That(disassembler.RelatedSkillIds, Does.Contain("skill.disassembly"));
            Assert.That(salvager.RelatedSkillIds, Does.Contain("skill.salvaging"));
            Assert.That(registry.TryGet(PrototypeProfessionDefinitionFactory.ItemRecoveryActivityDefinitionId, out ProfessionalActivityDefinition disassemblyActivity), Is.True);
            Assert.That(registry.TryGet(PrototypeProfessionDefinitionFactory.SalvagePickupActivityDefinitionId, out ProfessionalActivityDefinition salvageActivity), Is.True);
            Assert.That(disassemblyActivity.RequiredActivityTags, Does.Contain("recovery.disassemble"));
            Assert.That(disassemblyActivity.ApplicableProfessionIds, Does.Not.Contain(PrototypeProfessionDefinitionFactory.SalvagerProfessionId));
            Assert.That(salvageActivity.RequiredActivityTags, Does.Contain("recovery.salvage"));
            Assert.That(salvageActivity.ApplicableProfessionIds, Does.Contain(PrototypeProfessionDefinitionFactory.SalvagerProfessionId));
        }

        [Test]
        public void NaturalDecompositionUsesConfiguredConditionBandsAndPersistsSchedules()
        {
            DisassemblyRuntime runtime = new DisassemblyRuntime();
            NaturalDecompositionSettings settings = new NaturalDecompositionSettings { slowDurationSeconds = 1000d, fastDurationSeconds = 100d };
            NaturalDecompositionRecordData slow = runtime.ScheduleNaturalDecomposition("item.slow", "world.slow", "scene.test", 50d, 0.11f, settings);
            NaturalDecompositionRecordData fast = runtime.ScheduleNaturalDecomposition("item.fast", "world.fast", "scene.test", 50d, 0.10f, settings);
            NaturalDecompositionRecordData immediate = runtime.ScheduleNaturalDecomposition("item.immediate", "world.immediate", "scene.test", 50d, 0.05f, settings);

            Assert.That(slow.rate, Is.EqualTo(NaturalDecompositionRate.Slow));
            Assert.That(slow.dueAtWorldSeconds, Is.EqualTo(1050d));
            Assert.That(fast.rate, Is.EqualTo(NaturalDecompositionRate.Fast));
            Assert.That(fast.dueAtWorldSeconds, Is.EqualTo(150d));
            Assert.That(immediate.rate, Is.EqualTo(NaturalDecompositionRate.Immediate));
            Assert.That(runtime.IsNaturalDecompositionDue("item.immediate", 50d, out _), Is.True);
            Assert.That(runtime.IsNaturalDecompositionDue("item.fast", 149.99d, out _), Is.False);
            NaturalDecompositionRecordData overdueDamaged = runtime.ScheduleNaturalDecomposition("item.slow", "world.slow", "scene.test", 1200d, 0.05f, settings);
            Assert.That(overdueDamaged.rate, Is.EqualTo(NaturalDecompositionRate.Immediate));
            Assert.That(overdueDamaged.dueAtWorldSeconds, Is.EqualTo(1200d));

            DisassemblyRuntime restored = new DisassemblyRuntime();
            Assert.That(restored.RestoreFromSaveData(runtime.CreateSaveData(), null).Succeeded, Is.True);
            Assert.That(restored.NaturalDecompositions.Count, Is.EqualTo(3));
            Assert.That(restored.CancelNaturalDecomposition("item.fast"), Is.True);
            Assert.That(restored.IsNaturalDecompositionDue("item.fast", 999d, out _), Is.False);
        }

        [Test]
        public void NatureUsesDisassemblyRulesWithoutCreatingInventoryOutputs()
        {
            Fixture fixture = CreateFixture();
            DisassemblyRequest request = Request(fixture.ItemId, "nature.operation", ItemRecoveryOperationKind.NaturalDecomposition, 0, false);
            request.actorPersonId = "actor.nature";
            request.ownerPersonId = string.Empty;
            request.materializeOutputIdentities = false;
            int identitiesBefore = fixture.Items.Snapshots.Count;

            DisassemblyResult result = fixture.Runtime.Execute(request, fixture.Registry, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Operation.operationKind, Is.EqualTo(ItemRecoveryOperationKind.NaturalDecomposition));
            Assert.That(result.Operation.actorPersonId, Is.EqualTo("actor.nature"));
            Assert.That(result.Operation.skillUsed, Is.False);
            Assert.That(result.Operation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds), Is.Empty);
            Assert.That(fixture.Items.Snapshots.Count, Is.EqualTo(identitiesBefore));
            Assert.That(fixture.Items.TryGetSnapshot(fixture.ItemId, out ItemInstanceSnapshot source), Is.True);
            Assert.That(source.LifecycleState, Is.EqualTo(ItemLifecycleState.Disassembled));
        }

        private static Fixture CreateFixture()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();
            Assert.That(registry.TryGet("item.prototype-sword", out ItemDefinition sword), Is.True);
            ItemInstanceIdentityRuntime items = new ItemInstanceIdentityRuntime();
            string itemId = items.CreateItem(sword, itemInstanceId: "11111111-1111-1111-1111-111111111111", ownerPersonId: PersonId, custodianPersonId: PersonId).Snapshot.ItemInstanceId;
            ItemCompositionRuntime compositions = new ItemCompositionRuntime();
            ItemCompositionOperationResult composition = compositions.SetComposition(items, registry, new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{itemId}", itemInstanceId = itemId, sourceItemDefinitionId = sword.Id,
                completeness = ItemCompositionCompleteness.Complete, source = "crafting.test", tags = new[] { "item.composition", "composition.crafted" },
                materials =
                {
                    Material("material.blade", "material.iron-ore", "item.prototype-iron-ore", "component.blade", 3f),
                    Material("material.hilt", "material.leather", "item.leather-strip", "component.hilt", 1f)
                },
                components =
                {
                    new ItemComponentEntryData { componentEntryId = "component.blade", componentName = "Blade", materialEntryIds = new[] { "material.blade" } },
                    new ItemComponentEntryData { componentEntryId = "component.hilt", componentName = "Hilt", materialEntryIds = new[] { "material.hilt" } }
                }
            }, ItemCompositionMutationPurpose.CraftingProduction);
            Assert.That(composition.Succeeded, Is.True, composition.Message);
            ItemQualityAffixRuntime quality = new ItemQualityAffixRuntime();
            ItemDurabilityRuntime durability = new ItemDurabilityRuntime();
            Assert.That(durability.EnsureDefaultDurability(items, compositions, quality, registry, itemId).Succeeded, Is.True);
            return new Fixture(registry, items, compositions, quality, durability, new DisassemblyRuntime(), itemId);
        }

        private static ItemMaterialEntryData Material(string entryId, string materialId, string sourceDefinitionId, string componentId, float quantity) => new ItemMaterialEntryData
        {
            entryId = entryId, materialDefinitionId = materialId, sourceItemDefinitionId = sourceDefinitionId, componentEntryId = componentId,
            quantity = new MaterialQuantityData { value = quantity, unit = MaterialQuantityUnit.Count }, purity = 1f
        };

        private static DisassemblyRequest Request(string itemId, string operationId, ItemRecoveryOperationKind kind, int grade, bool used) => new DisassemblyRequest
        {
            operationId = operationId, itemInstanceId = itemId, actorPersonId = PersonId, ownerPersonId = PersonId, deterministicSeed = operationId,
            operationKind = kind, skillId = "skill.disassembly", skillGrade = grade, skillUsed = used
        };

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability, DisassemblyRuntime runtime, string itemId)
            { Registry = registry; Items = items; Compositions = compositions; Quality = quality; Durability = durability; Runtime = runtime; ItemId = itemId; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemCompositionRuntime Compositions { get; }
            public ItemQualityAffixRuntime Quality { get; }
            public ItemDurabilityRuntime Durability { get; }
            public DisassemblyRuntime Runtime { get; }
            public string ItemId { get; }
        }
    }
}
