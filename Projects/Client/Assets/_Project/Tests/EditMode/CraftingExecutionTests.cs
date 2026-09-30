using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class CraftingExecutionTests
    {
        [Test]
        public void PreviewIsReadonlyAndExecutionCreatesCompleteOutputGraph()
        {
            Fixture fixture = Fixture.Create();
            CraftingExecutionRequest request = fixture.Request("complete");

            CraftingExecutionResult preview = fixture.Crafting.Preview(request, fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Durability);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(fixture.Crafting.OperationCount, Is.Zero);
            Assert.That(fixture.Items.Count, Is.Zero);
            Assert.That(fixture.Production.PlanCount, Is.Zero);

            CraftingExecutionResult execute = fixture.Crafting.Execute(request, fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            string outputItem = execute.Operation.outputs.Single(output => output.createdItemInstance).itemInstanceId;
            Assert.That(fixture.Items.TryGetSnapshot(outputItem, out ItemInstanceSnapshot item), Is.True);
            Assert.That(item.ItemDefinitionId, Is.EqualTo(Fixture.SwordId));
            Assert.That(fixture.Compositions.TryGetSnapshotForItem(outputItem, out _), Is.True);
            Assert.That(fixture.Quality.TryGetQualityForItem(outputItem, out _), Is.True);
            Assert.That(fixture.Durability.TryGetDurabilityForItem(outputItem, out _), Is.True);
            Assert.That(fixture.Production.Plans.Any(plan => plan.status == ProductionPlanStatus.Released), Is.True);
        }

        [Test]
        public void DuplicateOperationReturnsCommittedSnapshotWithoutDuplicateOutput()
        {
            Fixture fixture = Fixture.Create();
            CraftingExecutionRequest request = fixture.Request("duplicate");

            CraftingExecutionResult first = fixture.Crafting.Execute(request, fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);
            int countAfterFirst = fixture.Items.Count;
            CraftingExecutionResult duplicate = fixture.Crafting.Execute(request, fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Items.Count, Is.EqualTo(countAfterFirst));
            Assert.That(duplicate.Operation.outputs.Select(output => output.itemInstanceId), Is.EqualTo(first.Operation.outputs.Select(output => output.itemInstanceId)));
        }

        [Test]
        public void FailedOutputCreationRollsBackReservedPlanAndItemMutations()
        {
            Fixture fixture = Fixture.Create();
            RecipeDefinition broken = Fixture.BrokenOutputRecipe(fixture.Iron);
            fixture.Registry = Fixture.Extend(fixture.Registry, broken);
            int itemCount = fixture.Items.Count;
            int planCount = fixture.Production.PlanCount;

            CraftingExecutionRequest request = fixture.Request("rollback", broken);
            CraftingExecutionResult result = fixture.Crafting.Execute(request, fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(CraftingExecutionStatus.OutputCreationFailed));
            Assert.That(fixture.Items.Count, Is.EqualTo(itemCount));
            Assert.That(fixture.Production.PlanCount, Is.EqualTo(planCount));
            Assert.That(fixture.Crafting.OperationCount, Is.Zero);
        }

        [Test]
        public void PersistenceParticipantValidatesOperationGraphBeforeCommit()
        {
            Fixture fixture = Fixture.Create();
            CraftingExecutionResult execute = fixture.Crafting.Execute(fixture.Request("persist"), fixture.Registry, fixture.Recipes, fixture.Production, fixture.Items, fixture.Compositions, fixture.Quality, fixture.Durability);
            Assert.That(execute.Succeeded, Is.True, execute.Message);

            CraftingExecutionRuntimeSaveData save = fixture.Crafting.CreateSaveData();
            CraftingExecutionRuntime restored = new CraftingExecutionRuntime();
            CraftingExecutionResult restore = restored.RestoreFromSaveData(save, fixture.Registry);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.OperationCount, Is.EqualTo(1));

            CraftingExecutionRuntimeSaveData corrupt = save.Clone();
            corrupt.operations[0].recipeId = "recipe.prototype.missing";
            CraftingExecutionPersistenceParticipant participant = new CraftingExecutionPersistenceParticipant(fixture.Crafting, () => fixture.Registry, "world.test");
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), CraftingExecutionPersistenceParticipant.CurrentParticipantSchemaVersion);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Crafting.OperationCount, Is.EqualTo(1));
        }

        private sealed class Fixture
        {
            public const string SwordId = "item.prototype.test-sword";

            public DefinitionRegistry Registry;
            public ItemDefinition Sword;
            public MaterialDefinition Iron;
            public RecipeDefinition Recipe;
            public RecipeRuntime Recipes = new RecipeRuntime();
            public ProductionRequirementRuntime Production = new ProductionRequirementRuntime();
            public ItemInstanceIdentityRuntime Items = new ItemInstanceIdentityRuntime();
            public ItemCompositionRuntime Compositions = new ItemCompositionRuntime();
            public ItemQualityAffixRuntime Quality = new ItemQualityAffixRuntime();
            public ItemDurabilityRuntime Durability = new ItemDurabilityRuntime();
            public CraftingExecutionRuntime Crafting = new CraftingExecutionRuntime();

            public static Fixture Create()
            {
                Fixture fixture = new Fixture();
                fixture.Sword = Item(SwordId, "Prototype Test Sword");
                fixture.Iron = Material("material.prototype.test-iron");
                fixture.Recipe = RecipeDefinition(fixture.Sword, fixture.Iron);
                fixture.Registry = new DefinitionRegistry(new IGameDefinition[] { fixture.Sword, fixture.Iron, fixture.Recipe });
                return fixture;
            }

            public CraftingExecutionRequest Request(string slug, RecipeDefinition recipe = null)
            {
                RecipeDefinition resolvedRecipe = recipe ?? Recipe;
                return new CraftingExecutionRequest
                {
                    operationId = $"crafting-operation.test.{slug}",
                    recipeId = resolvedRecipe.Id,
                    actorPersonId = "person.prototype.crafter",
                    ownerPersonId = "person.prototype.crafter",
                    custodianPersonId = "person.prototype.crafter",
                    locationId = "location.prototype.workbench",
                    worldTime = $"world-time.{slug}",
                    deterministicSeed = $"seed.{slug}",
                    productionContext = new ProductionContextData
                    {
                        actorPersonId = "person.prototype.crafter",
                        locationId = "location.prototype.workbench",
                        worldTime = $"world-time.{slug}",
                        materialQuantities =
                        {
                            new ProductionQuantityData { definitionId = Iron.Id, sourceContainerId = $"container.materials.{slug}", quantity = 4f, sourceTotalQuantity = 4f, unit = ProductionQuantityUnit.Kilogram }
                        }
                    }
                };
            }

            public static DefinitionRegistry Extend(DefinitionRegistry registry, params IGameDefinition[] additions)
            {
                return new DefinitionRegistry(registry.DefinitionsById.Values
                    .Where(definition => additions.All(addition => !string.Equals(addition.Id, definition.Id, StringComparison.Ordinal)))
                    .Concat(additions));
            }

            private static ItemDefinition Item(string id, string displayName)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", id);
                SetPrivate(item, "displayName", displayName);
                SetPrivate(item, "stackable", false);
                return item;
            }

            private static MaterialDefinition Material(string id)
            {
                MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
                SetPrivate(material, "materialId", id);
                SetPrivate(material, "displayName", id);
                SetPrivate(material, "category", MaterialCategory.Metal);
                SetPrivate(material, "physicalProperties", new MaterialPhysicalPropertySet
                {
                    densityKgPerLiter = 7.8f,
                    hardness = 0.8f,
                    durability = 0.8f,
                    flexibility = 0.2f
                });
                return material;
            }

            private static RecipeDefinition RecipeDefinition(ItemDefinition sword, MaterialDefinition iron)
            {
                RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                SetPrivate(recipe, "recipeId", "recipe.prototype.test-sword");
                SetPrivate(recipe, "displayName", "Prototype Test Sword Recipe");
                SetPrivate(recipe, "category", RecipeCategory.Smithing);
                SetPrivate(recipe, "currentVersionId", "recipe-version.prototype.test-sword.v1");
                SetPrivate(recipe, "versions", new[] { new RecipeVersionData { versionId = "recipe-version.prototype.test-sword.v1" } });
                SetPrivate(recipe, "inputs", new[]
                {
                    new RecipeInputSpecificationData { inputId = "input.iron", role = RecipeInputRole.PrimaryMaterial, materialDefinitionId = iron.Id, quantity = 2f, unit = ProductionQuantityUnit.Kilogram }
                });
                SetPrivate(recipe, "outputs", new[]
                {
                    new RecipeOutputSpecificationData { outputId = "output.sword", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = sword.Id, quantity = 1f }
                });
                SetPrivate(recipe, "procedureSteps", new[] { new RecipeProcedureStepData { stepId = "step.shape", stepKind = RecipeProcedureStepKind.Shape } });
                SetPrivate(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Fixed, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 1f, batchIncrement = 1f });
                return recipe;
            }

            public static RecipeDefinition BrokenOutputRecipe(MaterialDefinition iron)
            {
                RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                SetPrivate(recipe, "recipeId", "recipe.prototype.test-broken-output");
                SetPrivate(recipe, "displayName", "Broken Output Recipe");
                SetPrivate(recipe, "category", RecipeCategory.Smithing);
                SetPrivate(recipe, "currentVersionId", "recipe-version.prototype.test-broken-output.v1");
                SetPrivate(recipe, "versions", new[] { new RecipeVersionData { versionId = "recipe-version.prototype.test-broken-output.v1" } });
                SetPrivate(recipe, "inputs", new[]
                {
                    new RecipeInputSpecificationData { inputId = "input.iron", role = RecipeInputRole.PrimaryMaterial, materialDefinitionId = iron.Id, quantity = 1f, unit = ProductionQuantityUnit.Kilogram }
                });
                SetPrivate(recipe, "outputs", new[]
                {
                    new RecipeOutputSpecificationData { outputId = "output.missing", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = "item.prototype.missing", quantity = 1f }
                });
                SetPrivate(recipe, "procedureSteps", new[] { new RecipeProcedureStepData { stepId = "step.shape", stepKind = RecipeProcedureStepKind.Shape } });
                SetPrivate(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Fixed, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 1f, batchIncrement = 1f });
                return recipe;
            }

            private static void SetPrivate(object target, string fieldName, object value)
            {
                target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
            }
        }
    }
}
