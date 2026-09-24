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
    public sealed class ProductionChainsBatchWorkTests
    {
        [Test]
        public void ChainDefinitionsValidateVersionsStageGraphsAndImmutableSnapshots()
        {
            Fixture fixture = Fixture.Create();
            DefinitionCatalog catalog = Fixture.Catalog(fixture.ItemCategory, fixture.Rarity, fixture.Sword, fixture.Iron, fixture.Recipe, fixture.Chain);
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            ProductionChainVersionData snapshot = fixture.Chain.Versions.Single();
            snapshot.stages[0].stageId = "mutated";

            ProductionChainDefinition cycle = Fixture.CreateChainDefinition("production-chain.test.cycle", fixture.Recipe.Id, cyclic: true);
            DefinitionValidationReport cycleReport = DefinitionCatalogValidator.Validate(Fixture.Catalog(fixture.ItemCategory, fixture.Rarity, fixture.Sword, fixture.Iron, fixture.Recipe, cycle));

            Assert.That(report.ErrorCount, Is.Zero, string.Join(Environment.NewLine, report.Messages.Select(message => message.Message)));
            Assert.That(cycleReport.ErrorCount, Is.GreaterThan(0));
            Assert.That(fixture.Chain.Versions.Single().stages[0].stageId, Is.EqualTo("stage.prepare"));
        }

        [Test]
        public void WorkOrderJobQueueAndPriorityAreAtomicAndDeterministic()
        {
            Fixture fixture = Fixture.Create();
            fixture.Workflow.EnsureQueue("queue.production.test");
            ProductionWorkflowResult low = fixture.Workflow.CreateWorkOrder(fixture.WorkOrder("low", priority: 1), fixture.Registry);
            ProductionWorkflowResult high = fixture.Workflow.CreateWorkOrder(fixture.WorkOrder("high", priority: 10), fixture.Registry);
            fixture.Workflow.TransitionWorkOrder(low.WorkOrder.workOrderId, ProductionWorkOrderState.Approved);
            fixture.Workflow.TransitionWorkOrder(high.WorkOrder.workOrderId, ProductionWorkOrderState.Approved);
            ProductionWorkflowResult lowJob = fixture.Workflow.CreateJobFromWorkOrder("production-job.test.low", low.WorkOrder.workOrderId, fixture.Registry, "queue.production.test");
            ProductionWorkflowResult highJob = fixture.Workflow.CreateJobFromWorkOrder("production-job.test.high", high.WorkOrder.workOrderId, fixture.Registry, "queue.production.test");

            ProductionQueueData queue = fixture.Workflow.Queues.Single();

            Assert.That(low.Succeeded, Is.True, low.Message);
            Assert.That(high.Succeeded, Is.True, high.Message);
            Assert.That(lowJob.Succeeded, Is.True, lowJob.Message);
            Assert.That(highJob.Succeeded, Is.True, highJob.Message);
            Assert.That(queue.jobIds, Is.EqualTo(new[] { "production-job.test.high", "production-job.test.low" }));
            Assert.That(fixture.Workflow.BatchCount, Is.EqualTo(2));
        }

        [Test]
        public void WorldTimeProgressionIsExplicitIdempotentAndPauseSafe()
        {
            Fixture fixture = Fixture.CreateStartedJob();
            ProductionWorkflowResult first = fixture.Workflow.EvaluateJobToWorldTime(fixture.JobId, "1");
            long firstRevision = fixture.Workflow.Revision;
            ProductionWorkflowResult duplicate = fixture.Workflow.EvaluateJobToWorldTime(fixture.JobId, "1");
            long duplicateRevision = fixture.Workflow.Revision;
            fixture.Workflow.PauseJob(fixture.JobId, "manual", "1");
            long pauseRevision = fixture.Workflow.Revision;
            ProductionWorkflowResult paused = fixture.Workflow.EvaluateJobToWorldTime(fixture.JobId, "5");

            fixture.Workflow.TryGetJob(fixture.JobId, out ProductionJobData job);
            ProductionStageProgressData stage = job.stages.Single(stage => stage.stageId == fixture.StageId);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(firstRevision, Is.EqualTo(duplicateRevision));
            Assert.That(paused.Succeeded, Is.True, paused.Message);
            Assert.That(fixture.Workflow.Revision, Is.EqualTo(pauseRevision));
            Assert.That(stage.completedWork, Is.EqualTo(1f));
        }

        [Test]
        public void StageCompletionCreatesIntermediateBatchLotAndOutputOnce()
        {
            Fixture fixture = Fixture.CreateStartedJob();
            fixture.Workflow.EvaluateJobToWorldTime(fixture.JobId, "2");

            ProductionWorkflowResult complete = fixture.CompleteCurrentStage("2");
            int itemCount = fixture.Items.Count;
            ProductionWorkflowResult duplicate = fixture.CompleteCurrentStage("2");
            fixture.Workflow.TryGetJob(fixture.JobId, out ProductionJobData job);

            Assert.That(complete.Succeeded, Is.True, complete.Message);
            Assert.That(duplicate.Duplicate, Is.True, duplicate.Message);
            Assert.That(fixture.Workflow.BatchCount, Is.EqualTo(1));
            Assert.That(fixture.Workflow.LotCount, Is.EqualTo(1));
            Assert.That(fixture.Workflow.IntermediateCount, Is.EqualTo(1));
            Assert.That(fixture.Items.Count, Is.EqualTo(itemCount));
            Assert.That(job.completedStageIds, Does.Contain(fixture.StageId));
            Assert.That(job.outputItemIds.Length, Is.GreaterThan(0));
            Assert.That(fixture.Items.TryGetSnapshot(job.outputItemIds[0], out _), Is.True);
        }

        [Test]
        public void LotSplitMergeAndIncompatibleMergePreserveConservation()
        {
            Fixture fixture = Fixture.Create();
            ProductionWorkflowResult source = fixture.Workflow.CreateLot(new ProductionLotData { lotId = "lot.iron", definitionOrMaterialId = fixture.Iron.Id, quantity = 10f, unit = ProductionQuantityUnit.Kilogram });
            ProductionWorkflowResult split = fixture.Workflow.SplitLot("lot.iron", "lot.iron.child", 3f);
            ProductionWorkflowResult merged = fixture.Workflow.MergeLots("lot.iron.merged", new[] { "lot.iron", "lot.iron.child" });
            ProductionWorkflowResult wood = fixture.Workflow.CreateLot(new ProductionLotData { lotId = "lot.wood", definitionOrMaterialId = "material.wood", quantity = 2f, unit = ProductionQuantityUnit.Kilogram });
            ProductionWorkflowResult incompatible = fixture.Workflow.MergeLots("lot.invalid", new[] { "lot.iron.merged", "lot.wood" });

            Assert.That(source.Succeeded, Is.True, source.Message);
            Assert.That(split.Succeeded, Is.True, split.Message);
            Assert.That(merged.Succeeded, Is.True, merged.Message);
            Assert.That(wood.Succeeded, Is.True, wood.Message);
            Assert.That(incompatible.Succeeded, Is.False);
            Assert.That(merged.Lot.quantity, Is.EqualTo(10f));
            Assert.That(merged.Lot.parentLotIds, Is.EqualTo(new[] { "lot.iron", "lot.iron.child" }));
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptReferencesWithoutMutatingLiveRuntime()
        {
            Fixture fixture = Fixture.CreateStartedJob();
            ProductionWorkflowRuntimeSaveData save = fixture.Workflow.CreateSaveData();
            ProductionWorkflowPersistenceParticipant participant = new ProductionWorkflowPersistenceParticipant(fixture.Workflow, () => fixture.Registry, "world.test");
            ProductionWorkflowRuntimeSaveData corrupt = save.Clone();
            corrupt.jobs[0].batchId = "production-batch.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), ProductionWorkflowPersistenceParticipant.CurrentParticipantSchemaVersion);
            ProductionWorkflowRuntime restored = new ProductionWorkflowRuntime();
            ProductionWorkflowResult restore = restored.RestoreFromSaveData(save, fixture.Registry);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Workflow.JobCount, Is.EqualTo(1));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Revision, Is.EqualTo(fixture.Workflow.Revision));
        }

        [Test]
        public void LifecycleProjectionAndCancellationBoundariesAreStable()
        {
            Fixture fixture = Fixture.CreateStartedJob();
            ProductionWorkflowResult interrupt = fixture.Workflow.InterruptJob(fixture.JobId, "tool-break", "1");
            ProductionWorkflowResult recover = fixture.Workflow.ResumeJob(fixture.JobId, "2");
            ProductionWorkflowResult cancel = fixture.Workflow.CancelJob(fixture.JobId, "manual", "3");
            ProductionWorkflowResult duplicateCancel = fixture.Workflow.CancelJob(fixture.JobId, "manual", "3");
            ProductionProjectionData publicView = fixture.Workflow.ProjectJob(fixture.JobId, ProductionProjectionAudience.PublicObserver);
            ProductionProjectionData debugView = fixture.Workflow.ProjectJob(fixture.JobId, ProductionProjectionAudience.PrivilegedDebug);
            fixture.Workflow.TryGetJob(fixture.JobId, out ProductionJobData job);

            Assert.That(interrupt.Succeeded, Is.True, interrupt.Message);
            Assert.That(recover.Succeeded, Is.True, recover.Message);
            Assert.That(cancel.Succeeded, Is.True, cancel.Message);
            Assert.That(duplicateCancel.Duplicate, Is.True);
            Assert.That(job.state, Is.EqualTo(ProductionJobState.Cancelled));
            Assert.That(publicView.Decision, Is.EqualTo(ProductionProjectionDecision.RedactedAccess));
            Assert.That(debugView.Decision, Is.EqualTo(ProductionProjectionDecision.FullAccess));
        }

        private sealed class Fixture
        {
            public const string SwordId = "item.production.test-sword";

            public DefinitionRegistry Registry;
            public ItemDefinition Sword;
            public CategoryDefinition ItemCategory;
            public RarityDefinition Rarity;
            public MaterialDefinition Iron;
            public RecipeDefinition Recipe;
            public ProductionChainDefinition Chain;
            public ProductionWorkflowRuntime Workflow = new ProductionWorkflowRuntime();
            public ProductionRequirementRuntime Production = new ProductionRequirementRuntime();
            public ItemInstanceIdentityRuntime Items = new ItemInstanceIdentityRuntime();
            public ItemCompositionRuntime Compositions = new ItemCompositionRuntime();
            public ItemQualityAffixRuntime Quality = new ItemQualityAffixRuntime();
            public ItemDurabilityRuntime Durability = new ItemDurabilityRuntime();
            public CraftingExecutionRuntime Crafting = new CraftingExecutionRuntime();
            public RecipeRuntime Recipes = new RecipeRuntime();
            public string JobId;
            public string StageId;

            public static Fixture Create()
            {
                Fixture fixture = new Fixture();
                fixture.ItemCategory = ClassificationTestFactory.CreateCategory("category.production.test-equipment", "Production Test Equipment", CategoryDomain.Item);
                fixture.Rarity = ClassificationTestFactory.CreateRarity("rarity.production.test-common", "Production Test Common", 1, isDefault: true);
                fixture.Sword = Item(SwordId, "Production Test Sword", fixture.ItemCategory, fixture.Rarity);
                fixture.Iron = Material("material.production.test-iron");
                fixture.Recipe = CreateRecipeDefinition(fixture.Sword, fixture.Iron);
                fixture.Chain = CreateChainDefinition("production-chain.test.sword", fixture.Recipe.Id);
                fixture.Registry = new DefinitionRegistry(new IGameDefinition[] { fixture.ItemCategory, fixture.Rarity, fixture.Sword, fixture.Iron, fixture.Recipe, fixture.Chain });
                return fixture;
            }

            public static Fixture CreateStartedJob()
            {
                Fixture fixture = Create();
                ProductionWorkflowResult order = fixture.Workflow.CreateWorkOrder(fixture.WorkOrder("started", 5), fixture.Registry);
                fixture.Workflow.TransitionWorkOrder(order.WorkOrder.workOrderId, ProductionWorkOrderState.Approved);
                fixture.JobId = "production-job.test.started";
                ProductionWorkflowResult job = fixture.Workflow.CreateJobFromWorkOrder(fixture.JobId, order.WorkOrder.workOrderId, fixture.Registry);
                fixture.StageId = job.Job.readyStageIds.First();
                ProductionWorkflowResult start = fixture.Workflow.StartStage(fixture.JobId, fixture.StageId, fixture.Production, fixture.Registry, fixture.Context("start"), "station.production.test", 1, "0");
                Assert.That(start.Succeeded, Is.True, start.Message);
                return fixture;
            }

            public ProductionWorkflowResult CompleteCurrentStage(string worldTime)
            {
                return Workflow.CompleteStage(JobId, StageId, Registry, Recipes, Production, Items, Compositions, Quality, Durability, Crafting, Context("complete"), worldTime);
            }

            public ProductionWorkOrderData WorkOrder(string slug, int priority)
            {
                return new ProductionWorkOrderData
                {
                    workOrderId = $"production-work-order.test.{slug}",
                    requesterPersonId = "person.production.requester",
                    chainDefinitionId = Chain.Id,
                    versionId = Chain.CurrentVersionId,
                    requestedQuantity = 1,
                    priority = priority,
                    ownerPersonId = "person.production.owner",
                    custodianPersonId = "person.production.custodian",
                    destinationId = "container.production.output"
                };
            }

            private ProductionContextData Context(string slug)
            {
                return new ProductionContextData
                {
                    actorPersonId = "person.production.worker",
                    locationId = "location.production.test",
                    worldTime = "0",
                    materialQuantities =
                    {
                        new ProductionQuantityData
                        {
                            definitionId = Iron.Id,
                            sourceContainerId = $"container.material.{slug}",
                            quantity = 20f,
                            sourceTotalQuantity = 20f,
                            unit = ProductionQuantityUnit.Kilogram
                        }
                    }
                };
            }

            public static DefinitionCatalog Catalog(params ScriptableObject[] definitions)
            {
                return ClassificationTestFactory.CreateCatalog(definitions);
            }

            private static ItemDefinition Item(string id, string displayName, CategoryDefinition category, RarityDefinition rarity)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", id);
                SetPrivate(item, "displayName", displayName);
                SetPrivate(item, "primaryCategory", category);
                SetPrivate(item, "rarity", rarity);
                SetPrivate(item, "stackable", false);
                return item;
            }

            private static MaterialDefinition Material(string id)
            {
                MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
                SetPrivate(material, "materialId", id);
                SetPrivate(material, "displayName", id);
                SetPrivate(material, "category", MaterialCategory.Metal);
                SetPrivate(material, "physicalProperties", new MaterialPhysicalPropertySet { densityKgPerLiter = 7.8f, hardness = 0.8f, durability = 0.8f, flexibility = 0.2f });
                return material;
            }

            private static RecipeDefinition CreateRecipeDefinition(ItemDefinition sword, MaterialDefinition iron)
            {
                RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                SetPrivate(recipe, "recipeId", "recipe.production.test-sword");
                SetPrivate(recipe, "displayName", "Production Test Sword Recipe");
                SetPrivate(recipe, "category", RecipeCategory.Smithing);
                SetPrivate(recipe, "currentVersionId", "recipe-version.production.test-sword.v1");
                SetPrivate(recipe, "versions", new[] { new RecipeVersionData { versionId = "recipe-version.production.test-sword.v1" } });
                SetPrivate(recipe, "inputs", new[] { new RecipeInputSpecificationData { inputId = "input.iron", role = RecipeInputRole.PrimaryMaterial, materialDefinitionId = iron.Id, quantity = 2f, unit = ProductionQuantityUnit.Kilogram } });
                SetPrivate(recipe, "outputs", new[] { new RecipeOutputSpecificationData { outputId = "output.sword", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = sword.Id, quantity = 1f } });
                SetPrivate(recipe, "procedureSteps", new[] { new RecipeProcedureStepData { stepId = "step.shape", stepKind = RecipeProcedureStepKind.Shape } });
                SetPrivate(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Fixed, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 1f, batchIncrement = 1f });
                return recipe;
            }

            public static ProductionChainDefinition CreateChainDefinition(string id, string recipeId, bool cyclic = false)
            {
                ProductionChainDefinition chain = ScriptableObject.CreateInstance<ProductionChainDefinition>();
                SetPrivate(chain, "chainId", id);
                SetPrivate(chain, "displayName", id);
                SetPrivate(chain, "category", "smithing");
                SetPrivate(chain, "currentVersionId", "production-chain-version.test.v1");
                ProductionStageDefinitionData prepare = new ProductionStageDefinitionData
                {
                    stageId = "stage.prepare",
                    recipeDefinitionId = recipeId,
                    recipeVersionId = "recipe-version.production.test-sword.v1",
                    category = ProductionStageCategory.Preparation,
                    dependencyStageIds = cyclic ? new[] { "stage.finish" } : Array.Empty<string>(),
                    requiredWorkUnits = 2f,
                    estimatedDuration = 2f
                };
                ProductionStageDefinitionData finish = new ProductionStageDefinitionData
                {
                    stageId = "stage.finish",
                    recipeDefinitionId = recipeId,
                    recipeVersionId = "recipe-version.production.test-sword.v1",
                    category = ProductionStageCategory.Assembly,
                    dependencyStageIds = new[] { "stage.prepare" },
                    requiredWorkUnits = 1f,
                    estimatedDuration = 1f
                };
                SetPrivate(chain, "versions", new[]
                {
                    new ProductionChainVersionData
                    {
                        versionId = "production-chain-version.test.v1",
                        chainDefinitionId = id,
                        stages = new[] { prepare, finish }
                    }
                });
                return chain;
            }

            private static void SetPrivate(object target, string fieldName, object value)
            {
                target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
            }
        }
    }
}
