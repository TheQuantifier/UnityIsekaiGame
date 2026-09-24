using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Integration;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step9ItemCraftingIntegrationFinalizationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string SwordId = "item.prototype-sword";

        [Test]
        public void AuthorityMapAndPersistenceDependenciesAreCompleteAndAcyclic()
        {
            Assert.That(Step9IntegrationValidator.AuthorityMap.Select(entry => entry.Domain).Distinct().Count(), Is.EqualTo(Step9IntegrationValidator.AuthorityMap.Count));

            Step9IntegrationValidationReport report = Step9IntegrationValidator.ValidateRuntimeGraph(new Step9IntegrationRuntimeSnapshot(), PrototypeRegistry());

            Assert.That(report.Diagnostics.Where(diagnostic => diagnostic.Domain == Step9IntegrationDiagnosticDomain.Authority && diagnostic.Severity == Step9IntegrationDiagnosticSeverity.Error), Is.Empty);
            Assert.That(report.Diagnostics.Where(diagnostic => diagnostic.Domain == Step9IntegrationDiagnosticDomain.Persistence && diagnostic.Severity == Step9IntegrationDiagnosticSeverity.Error), Is.Empty);
        }

        [Test]
        public void PrototypeCatalogProvidesStep9DefinitionsWithoutIntegrationErrors()
        {
            DefinitionRegistry registry = PrototypeRegistry();

            Step9IntegrationValidationReport report = Step9IntegrationValidator.ValidateDefinitions(registry);

            Assert.That(report.ErrorCount, Is.Zero, string.Join("\n", report.Diagnostics));
        }

        [Test]
        public void PrototypeCatalogProvidesAuthoredResourcesRecipesPoliciesAndWorkstation()
        {
            DefinitionRegistry registry = PrototypeRegistry();

            Assert.That(registry.TryGet("item.prototype-iron-ore", out ItemDefinition ironOre), Is.True);
            Assert.That(registry.TryGet("item.wood-log", out ItemDefinition woodLog), Is.True);
            Assert.That(ironOre.Stackable, Is.True);
            Assert.That(woodLog.Stackable, Is.True);
            Assert.That(registry.TryGet("production-station.prototype.workstation", out ProductionStationDefinition _), Is.True);
            Assert.That(registry.TryGet("composition-policy.input-derived", out CraftingOutputPolicyDefinition _), Is.True);
            Assert.That(registry.TryGet("quality-policy.crafting.standard", out CraftingOutputPolicyDefinition _), Is.True);
            Assert.That(registry.TryGet("affix-policy.crafting.standard", out CraftingOutputPolicyDefinition affixPolicy), Is.True);
            Assert.That(affixPolicy.AffixChance, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(registry.TryGet("recipe.prototype-sword", out RecipeDefinition sword), Is.True);
            Assert.That(sword.Tags, Does.Contain("craftable.sword"));
            Assert.That(sword.Inputs.Select(input => input.componentRoleId), Is.EquivalentTo(new[] { "component.blade", "component.guard", "component.hilt", "component.pommel" }));
            RecipeInputSpecificationData swordMetal = sword.Inputs.Single(input => input.componentRoleId == "component.blade");
            Assert.That(swordMetal.itemDefinitionId, Is.Empty);
            Assert.That(swordMetal.materialTagIds, Is.EquivalentTo(new[] { "material.metal" }));
            Assert.That(sword.Inputs.Where(input => input.materialTagIds.Contains("material.metal")).Sum(input => input.quantity), Is.EqualTo(3f));
            Assert.That(sword.Inputs.Single(input => input.componentRoleId == "component.hilt").materialTagIds, Is.EquivalentTo(new[] { "material.leather" }));
            Assert.That(RecipeInputMatcher.Matches(swordMetal, ironOre, registry), Is.True);
            Assert.That(RecipeInputMatcher.Matches(swordMetal, woodLog, registry), Is.False);
            Assert.That(RecipeInputMatcher.Describe(swordMetal, registry), Is.EqualTo("Any Metal"));
            Assert.That(registry.TryGet("recipe.prototype-arrows", out RecipeDefinition arrows), Is.True);
            Assert.That(arrows.Outputs.Single().quantity, Is.EqualTo(10f));
            Assert.That(arrows.Inputs.SelectMany(input => input.materialTagIds), Is.EquivalentTo(new[] { "material.metal", "material.wood" }));
            Assert.That(arrows.Inputs.Single(input => input.materialTagIds.Contains("material.wood")).itemDefinitionId, Is.Empty);
            Assert.That(registry.TryGet("item.health-potion", out ItemDefinition healthPotion), Is.True);
            Assert.That(RecipeInputMatcher.GetResourceCategoryIds(healthPotion, registry), Does.Contain("material.liquid"));
            Assert.That(RecipeInputMatcher.Matches(new RecipeInputSpecificationData { materialTagIds = new[] { "material.liquid" } }, healthPotion, registry), Is.False, "Finished consumables must not be treated as raw crafting resources.");
            Assert.That(registry.TryGet("crafting-catalyst.health-potion", out CraftingCatalystEffectDefinition healthCatalyst), Is.True);
            Assert.That(healthCatalyst.ResultingAffix.Id, Is.EqualTo("affix.catalyst-vitality"));
            Assert.That(healthCatalyst.CalculateChance(registry.DefinitionsById["item.health-potion"] as ItemDefinition, 3, 4), Is.GreaterThan(healthCatalyst.CalculateChance(registry.DefinitionsById["item.health-potion"] as ItemDefinition, 1, 0)));
            Assert.That(healthCatalyst.SelectAffixTierId(registry.DefinitionsById["item.health-potion"] as ItemDefinition, 5), Is.EqualTo("affix.catalyst-vitality.tier.3"));
        }

        [Test]
        public void MaterialFamilyRecipeAutomaticallyAcceptsNewMaterialsFromItemComposition()
        {
            DefinitionRegistry prototype = PrototypeRegistry();
            Assert.That(prototype.TryGet("recipe.prototype-sword", out RecipeDefinition sword), Is.True);
            Assert.That(prototype.TryGet("item.prototype-iron-ore", out ItemDefinition authoredResource), Is.True);
            RecipeInputSpecificationData metalInput = sword.Inputs.Single(input => input.componentRoleId == "component.blade");
            MaterialDefinition copper = ScriptableObject.CreateInstance<MaterialDefinition>();
            ItemDefinition copperOre = ScriptableObject.CreateInstance<ItemDefinition>();

            try
            {
                SetPrivate(copper, "materialId", "material.copper");
                SetPrivate(copper, "displayName", "Copper");
                SetPrivate(copper, "category", MaterialCategory.Metal);
                SetPrivate(copper, "materialTags", new[] { "material.metal" });
                SetPrivate(copperOre, "itemId", "item.test-copper-ore");
                SetPrivate(copperOre, "displayName", "Copper Ore");
                SetPrivate(copperOre, "primaryCategory", authoredResource.PrimaryCategory);
                SetPrivate(copperOre, "defaultCompositionTemplate", new ItemCompositionTemplateData
                {
                    materials =
                    {
                        new ItemMaterialEntryData
                        {
                            entryId = "material.primary",
                            materialDefinitionId = copper.Id,
                            quantity = new MaterialQuantityData { value = 1f, unit = MaterialQuantityUnit.Count }
                        }
                    }
                });

                DefinitionRegistry extended = new DefinitionRegistry(prototype.DefinitionsById.Values.Concat(new IGameDefinition[] { copper, copperOre }));

                Assert.That(RecipeInputMatcher.Matches(metalInput, copperOre, extended), Is.True);
                Assert.That(RecipeInputMatcher.FindMatchingItems(metalInput, extended).Select(item => item.Id), Does.Contain(copperOre.Id));
                Assert.That(RecipeInputMatcher.GetResourceCategoryIds(copperOre, extended), Does.Contain("material.metal"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copperOre);
                UnityEngine.Object.DestroyImmediate(copper);
            }
        }

        [Test]
        public void CatalystCraftingConsumesAssignedStackAndAppliesDeterministicRelatedAffix()
        {
            DefinitionRegistry registry = PrototypeRegistry();
            Assert.That(registry.TryGet("recipe.prototype-sword", out RecipeDefinition recipe), Is.True);
            Assert.That(registry.TryGet("item.prototype-iron-ore", out ItemDefinition ironOre), Is.True);
            Assert.That(registry.TryGet("item.leather-strip", out ItemDefinition leatherStrip), Is.True);
            Assert.That(registry.TryGet("item.health-potion", out ItemDefinition healthPotion), Is.True);
            Assert.That(registry.TryGet("production-station.prototype.workstation", out ProductionStationDefinition station), Is.True);

            ItemInstanceIdentityRuntime items = new ItemInstanceIdentityRuntime();
            const string personId = "person.prototype.crafter";
            const string ironId = "11111111-1111-1111-1111-111111111111";
            const string potionId = "22222222-2222-2222-2222-222222222222";
            const string leatherId = "33333333-3333-3333-3333-333333333333";
            Assert.That(items.CreateItem(ironOre, ItemInstanceClassification.StackableWhileEquivalent, ironId, ownerPersonId: personId, custodianPersonId: personId, stackQuantity: 3).Succeeded, Is.True);
            Assert.That(items.CreateItem(healthPotion, ItemInstanceClassification.StackableWhileEquivalent, potionId, ownerPersonId: personId, custodianPersonId: personId, stackQuantity: 11).Succeeded, Is.True);
            Assert.That(items.CreateItem(leatherStrip, ItemInstanceClassification.StackableWhileEquivalent, leatherId, ownerPersonId: personId, custodianPersonId: personId, stackQuantity: 1).Succeeded, Is.True);

            ProductionRequirementRuntime production = new ProductionRequirementRuntime();
            Assert.That(production.RegisterStation(station, "station.test", "location.prototype.merchant-counter").Succeeded, Is.True);
            ItemCompositionRuntime compositions = new ItemCompositionRuntime();
            ItemQualityAffixRuntime quality = new ItemQualityAffixRuntime();
            ItemDurabilityRuntime durability = new ItemDurabilityRuntime();
            CraftingExecutionRuntime crafting = new CraftingExecutionRuntime();
            string seed = Enumerable.Range(0, 100).Select(index => $"seed.catalyst.{index}").First(candidate => UnitInterval($"{candidate}:catalyst.optional.1:roll") <= 0.9f);
            CraftingExecutionRequest request = new CraftingExecutionRequest
            {
                operationId = "crafting-operation.test.catalyst",
                recipeId = recipe.Id,
                actorPersonId = personId,
                ownerPersonId = personId,
                custodianPersonId = personId,
                locationId = "location.prototype.merchant-counter",
                worldTime = "1",
                deterministicSeed = seed,
                craftingSkillUsed = true,
                craftingSkillId = "skill.smithing",
                craftingSkillGrade = 7,
                catalysts =
                {
                    new CraftingCatalystUseData { slotId = "catalyst.optional.1", itemDefinitionId = healthPotion.Id, itemInstanceId = potionId, quantity = 11 }
                },
                productionContext = new ProductionContextData
                {
                    actorPersonId = personId,
                    locationId = "location.prototype.merchant-counter",
                    worldTime = "1",
                    itemQuantities =
                    {
                        new ProductionQuantityData
                        {
                            definitionId = ironOre.Id,
                            itemInstanceId = ironId,
                            locationId = "location.prototype.merchant-counter",
                            quantity = 3f,
                            sourceTotalQuantity = 3f,
                            unit = ProductionQuantityUnit.Count,
                            expectedRuntimeRevision = items.Revision,
                            expectedStackRevision = 1L,
                            perceived = true,
                            authoritative = true
                        },
                        new ProductionQuantityData
                        {
                            definitionId = leatherStrip.Id,
                            itemInstanceId = leatherId,
                            locationId = "location.prototype.merchant-counter",
                            quantity = 1f,
                            sourceTotalQuantity = 1f,
                            unit = ProductionQuantityUnit.Count,
                            expectedRuntimeRevision = items.Revision,
                            expectedStackRevision = 1L,
                            perceived = true,
                            authoritative = true
                        }
                    }
                }
            };

            CraftingExecutionResult result = crafting.Execute(request, registry, new RecipeRuntime(), production, items, compositions, quality, durability);

            Assert.That(result.Succeeded, Is.True, result.Message);
            CraftingCatalystUseData catalyst = result.Operation.catalysts.Single();
            Assert.That(catalyst.consumed, Is.True);
            Assert.That(catalyst.effectChance, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(catalyst.effectApplied, Is.True);
            Assert.That(catalyst.affixDefinitionId, Is.EqualTo("affix.catalyst-vitality"));
            string outputId = result.Operation.outputs.Single(output => output.createdItemInstance).itemInstanceId;
            Assert.That(result.Operation.consumedInputs.Where(input => input.inputId != "input.hilt").All(input => input.definitionId == ironOre.Id), Is.True);
            Assert.That(result.Operation.consumedInputs.Single(input => input.inputId == "input.hilt").definitionId, Is.EqualTo(leatherStrip.Id));
            Assert.That(compositions.TryGetSnapshotForItem(outputId, out ItemCompositionSnapshot craftedComposition), Is.True);
            Assert.That(craftedComposition.Materials.Select(material => material.materialDefinitionId), Does.Contain("material.iron-ore"));
            Assert.That(craftedComposition.Components.Select(component => component.componentEntryId), Is.EquivalentTo(new[] { "component.blade", "component.guard", "component.hilt", "component.pommel" }));
            Assert.That(items.TryGetSnapshot(outputId, out ItemInstanceSnapshot craftedItem), Is.True);
            Assert.That(craftedItem.CustomName, Is.EqualTo("Iron Sword with Leather Grip"));
            Assert.That(quality.GetAffixesForItem(outputId, activeOnly: true).Any(affix => affix.AffixDefinitionId == "affix.catalyst-vitality"), Is.True);
            Assert.That(items.TryGetSnapshot(potionId, out ItemInstanceSnapshot consumedPotion), Is.True);
            Assert.That(consumedPotion.LifecycleState, Is.EqualTo(ItemLifecycleState.Consumed));
        }

        [Test]
        public void HigherCraftingSkillReducesTimeAndRaisesQualityAndAffixChance()
        {
            DefinitionRegistry registry = PrototypeRegistry();
            Assert.That(registry.TryGet("recipe.prototype-sword", out RecipeDefinition recipe), Is.True);
            Assert.That(registry.TryGet("skill.smithing", out SkillDefinition smithing), Is.True);
            GameObject host = new GameObject("CraftingScalingTest");
            try
            {
                CharacterSkillCollection skills = host.AddComponent<CharacterSkillCollection>();
                skills.Configure(registry);
                CraftingScalingCalculation unskilled = CraftingScalingCalculator.Calculate(recipe, registry, skills);
                Assert.That(skills.GrantSkill(smithing, SkillGrade.F, SkillAcquisitionSource.Development, "test").Succeeded, Is.True);
                CraftingScalingCalculation beginner = CraftingScalingCalculator.Calculate(recipe, registry, skills);
                Assert.That(skills.GrantSkill(smithing, SkillGrade.AAA, SkillAcquisitionSource.Development, "test").Succeeded, Is.True);
                CraftingScalingCalculation master = CraftingScalingCalculator.Calculate(recipe, registry, skills);

                Assert.That(beginner.FinalDurationSeconds, Is.LessThan(unskilled.FinalDurationSeconds));
                Assert.That(master.FinalDurationSeconds, Is.LessThan(beginner.FinalDurationSeconds));
                Assert.That(master.ExpectedQuality, Is.GreaterThan(beginner.ExpectedQuality));
                Assert.That(master.ExpectedEquipmentStatMultiplier, Is.GreaterThan(beginner.ExpectedEquipmentStatMultiplier));
                Assert.That(master.AffixChanceBonus, Is.GreaterThan(beginner.AffixChanceBonus));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeGraphDetectsCrossRuntimeConflictsAndMissingReferences()
        {
            DefinitionRegistry registry = PrototypeRegistry();
            ItemInstanceRuntimeSaveData items = new ItemInstanceRuntimeSaveData
            {
                records =
                {
                    Item("item.instance.a", ItemLifecycleState.Active, ItemLocationKind.Equipped, "person.owner", "main-hand"),
                    Item("item.instance.b", ItemLifecycleState.Active, ItemLocationKind.Equipped, "person.owner", "main-hand"),
                    Item("item.instance.dead", ItemLifecycleState.Disassembled, ItemLocationKind.Inventory, "person.owner", "")
                }
            };
            ItemCompositionRuntimeSaveData compositions = new ItemCompositionRuntimeSaveData
            {
                records =
                {
                    new ItemCompositionRecordData
                    {
                        compositionId = "composition.a",
                        itemInstanceId = "item.instance.a",
                        sourceItemDefinitionId = SwordId,
                        components =
                        {
                            new ItemComponentEntryData { componentEntryId = "component.shared", componentItemInstanceId = "item.instance.missing" }
                        }
                    }
                }
            };
            ItemDurabilityRuntimeSaveData durability = new ItemDurabilityRuntimeSaveData
            {
                records =
                {
                    new ItemDurabilityRecordData
                    {
                        durabilityRecordId = "durability.a",
                        itemInstanceId = "item.instance.a",
                        itemDefinitionId = SwordId,
                        currentDurability = 150f,
                        maximumDurability = 100f
                    }
                }
            };

            Step9IntegrationValidationReport report = Step9IntegrationValidator.ValidateRuntimeGraph(
                new Step9IntegrationRuntimeSnapshot(itemInstances: items, itemCompositions: compositions, itemDurability: durability),
                registry);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "DuplicateExclusiveLocation"), Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "TerminalItemHasActiveLocation"), Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "TrackedComponentMissing"), Is.True);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "InvalidDurabilityRange"), Is.True);
        }

        [Test]
        public void SaveSchemaValidationRejectsUnsupportedStep9VersionBeforeRuntimeRestore()
        {
            DefinitionRegistry registry = PrototypeRegistry();
            ItemInstanceRuntimeSaveData items = new ItemInstanceRuntimeSaveData { schemaVersion = ItemInstanceRuntimeSaveData.CurrentSchemaVersion + 1 };

            Step9IntegrationValidationReport report = Step9IntegrationValidator.ValidateRuntimeGraph(new Step9IntegrationRuntimeSnapshot(itemInstances: items), registry);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Diagnostics.Any(diagnostic => diagnostic.Code == "UnsupportedSchemaVersion" && diagnostic.SubjectId == "ItemInstanceIdentityRuntime"), Is.True);
        }

        [Test]
        public void CanonicalFingerprintIsDeterministicAndOrderIndependent()
        {
            ItemInstanceRuntimeSaveData first = new ItemInstanceRuntimeSaveData
            {
                records =
                {
                    Item("item.instance.b", ItemLifecycleState.Active, ItemLocationKind.Inventory, "person.owner", ""),
                    Item("item.instance.a", ItemLifecycleState.Active, ItemLocationKind.Inventory, "person.owner", "")
                }
            };
            ItemInstanceRuntimeSaveData second = first.Clone();
            second.records.Reverse();

            string firstFingerprint = Step9IntegrationValidator.CreateCanonicalFingerprint(new Step9IntegrationRuntimeSnapshot(itemInstances: first));
            string secondFingerprint = Step9IntegrationValidator.CreateCanonicalFingerprint(new Step9IntegrationRuntimeSnapshot(itemInstances: second));

            Assert.That(firstFingerprint, Is.EqualTo(secondFingerprint));
        }

        [Test]
        public void RuntimeSnapshotClonesInputsAndRemainsImmutableAfterSourceMutation()
        {
            ItemInstanceRuntimeSaveData source = new ItemInstanceRuntimeSaveData
            {
                records = { Item("item.instance.a", ItemLifecycleState.Active, ItemLocationKind.Inventory, "person.owner", "") }
            };
            Step9IntegrationRuntimeSnapshot snapshot = new Step9IntegrationRuntimeSnapshot(itemInstances: source);

            source.records[0].itemInstanceId = "item.instance.changed";
            string fingerprint = Step9IntegrationValidator.CreateCanonicalFingerprint(snapshot);

            Assert.That(fingerprint, Is.EqualTo(Step9IntegrationValidator.CreateCanonicalFingerprint(snapshot.Clone())));
            Assert.That(snapshot.ItemInstances.records.Single().itemInstanceId, Is.EqualTo("item.instance.a"));
        }

        private static ItemInstanceRecordData Item(string itemId, ItemLifecycleState lifecycle, ItemLocationKind location, string holderOrOwner, string slot)
        {
            return new ItemInstanceRecordData
            {
                itemInstanceId = itemId,
                itemDefinitionId = SwordId,
                classification = ItemInstanceClassification.IndividuallyTracked,
                stackQuantity = 1,
                lifecycleState = lifecycle,
                location = location switch
                {
                    ItemLocationKind.Equipped => new ItemLocationStateData { kind = location, equipmentHolderId = holderOrOwner, equipmentSlotId = slot },
                    ItemLocationKind.Inventory => new ItemLocationStateData { kind = location, inventoryOwnerId = holderOrOwner },
                    _ => new ItemLocationStateData { kind = location }
                }
            };
        }

        private static DefinitionRegistry PrototypeRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            return catalog.CreateRegistry();
        }

        private static float UnitInterval(string seed)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
            return BitConverter.ToUInt32(bytes, 0) / (float)uint.MaxValue;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
