using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class RecipesCraftingKnowledgeTests
    {
        [Test]
        public void RecipeDefinition_ValidatesVersionsVariantsProcedureInputsAndOutputs()
        {
            RecipeFixture fixture = CreateFixture();
            DefinitionValidationReport report = new DefinitionValidationReport();

            fixture.Recipe.ValidateCatalogDefinition(fixture.Definitions.ToDictionary(definition => definition.Id), report);

            Assert.That(report.ErrorCount, Is.Zero, string.Join(Environment.NewLine, report.Messages.Select(message => message.Message)));
        }

        [Test]
        public void RecipeDefinition_RejectsProcedureCyclesAndMissingReferences()
        {
            RecipeFixture fixture = CreateFixture();
            SetField(fixture.Recipe, "procedureSteps", new[]
            {
                Step("recipe-step.a", RecipeProcedureStepKind.PrepareInput, "recipe-step.b"),
                Step("recipe-step.b", RecipeProcedureStepKind.AssembleComponent, "recipe-step.a")
            });
            DefinitionValidationReport report = new DefinitionValidationReport();

            fixture.Recipe.ValidateCatalogDefinition(fixture.Definitions.ToDictionary(definition => definition.Id), report);

            Assert.That(report.ErrorCount, Is.GreaterThan(0));
            Assert.That(report.Messages.Any(message => message.Message.Contains("dependency cycle")), Is.True);
        }

        [Test]
        public void PreviewBuildsExactProductionPlanWithoutMutatingRuntime()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeRuntime runtime = new RecipeRuntime();
            ProductionContextData context = new ProductionContextData
            {
                actorPersonId = "person.smith",
                locationId = "location.workshop",
                materialQuantities =
                {
                    new ProductionQuantityData
                    {
                        definitionId = fixture.Iron.Id,
                        sourceContainerId = "container.materials",
                        quantity = 5f,
                        sourceTotalQuantity = 5f,
                        unit = ProductionQuantityUnit.Kilogram
                    },
                    new ProductionQuantityData
                    {
                        definitionId = fixture.SecretCatalyst.Id,
                        sourceContainerId = "container.secret",
                        quantity = 2f,
                        sourceTotalQuantity = 2f,
                        unit = ProductionQuantityUnit.Kilogram
                    }
                }
            };

            RecipeResolutionResult result = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = fixture.Recipe.Id,
                batchSize = 1f,
                productionContext = context,
                buildRequirementPlan = true,
                reservePlan = false
            }, fixture.Registry, fixture.Production, fixture.Items, fixture.Durability);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.True);
            Assert.That(fixture.Production.PlanCount, Is.Zero);
            Assert.That(result.RequirementResult.Plan.selections.Any(selection => selection.requirementType == ProductionRequirementType.Material && Math.Abs(selection.quantity - 2f) < 0.001f), Is.True);
        }

        [Test]
        public void ReservationCommitsPlanAndReservationOnlyWhenRequested()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeRuntime runtime = new RecipeRuntime();
            ProductionContextData context = new ProductionContextData
            {
                actorPersonId = "person.smith",
                locationId = "location.workshop",
                materialQuantities =
                {
                    new ProductionQuantityData
                    {
                        definitionId = fixture.Iron.Id,
                        sourceContainerId = "container.materials",
                        quantity = 5f,
                        sourceTotalQuantity = 5f,
                        unit = ProductionQuantityUnit.Kilogram
                    },
                    new ProductionQuantityData
                    {
                        definitionId = fixture.SecretCatalyst.Id,
                        sourceContainerId = "container.secret",
                        quantity = 2f,
                        sourceTotalQuantity = 2f,
                        unit = ProductionQuantityUnit.Kilogram
                    }
                }
            };

            RecipeResolutionResult result = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = fixture.Recipe.Id,
                batchSize = 1f,
                productionContext = context,
                reservePlan = true,
                productionJobId = "production-job.prototype.recipe"
            }, fixture.Registry, fixture.Production, fixture.Items, fixture.Durability);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Preview, Is.False);
            Assert.That(fixture.Production.PlanCount, Is.EqualTo(1));
            Assert.That(fixture.Production.ReservationCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void HiddenInputsAreRedactedFromOrdinaryProjectionButAvailableToPrivilegedResolution()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeRuntime runtime = new RecipeRuntime();

            RecipeResolutionResult ordinary = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = fixture.Recipe.Id,
                batchSize = 1f,
                accessLevel = RecipeProjectionAccessLevel.Ordinary,
                buildRequirementPlan = false
            }, fixture.Registry);
            RecipeResolutionResult privileged = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = fixture.Recipe.Id,
                batchSize = 1f,
                accessLevel = RecipeProjectionAccessLevel.Privileged,
                buildRequirementPlan = false
            }, fixture.Registry);

            Assert.That(ordinary.Succeeded, Is.True);
            Assert.That(privileged.Succeeded, Is.True);
            Assert.That(ordinary.Snapshot.Redacted, Is.True);
            Assert.That(ordinary.Snapshot.Inputs.Any(input => input.hidden && string.IsNullOrWhiteSpace(input.materialDefinitionId)), Is.True);
            Assert.That(privileged.Snapshot.Inputs.Any(input => input.hidden && input.materialDefinitionId == fixture.SecretCatalyst.Id), Is.True);
        }

        [Test]
        public void PersonRecipeKnowledgeProjectsPartialOutdatedAndIncorrectKnowledgeWithoutMutatingTruth()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeRuntime recipes = new RecipeRuntime();
            RecipeKnowledgeRuntime knowledge = new RecipeKnowledgeRuntime();
            RecipeResolvedSnapshot truth = recipes.Resolve(new RecipeResolutionRequest { recipeId = fixture.Recipe.Id, buildRequirementPlan = false }, fixture.Registry).Snapshot;
            RecipeKnowledgeRecordData record = knowledge.LearnOrUpdate(new RecipeKnowledgeRecordData
            {
                recordId = "recipe-knowledge.person.smith.prototype-sword",
                personId = "person.smith",
                recipeId = fixture.Recipe.Id,
                versionId = "recipe-version.prototype-sword.v1",
                completeness = RecipeKnowledgeCompleteness.Partial,
                incorrect = true,
                outdated = true,
                knownInputIds = new[] { "recipe-input.iron" },
                knownOutputIds = new[] { "recipe-output.sword" },
                knownStepIds = new[] { "recipe-step.prepare" },
                sourceIds = new[] { "information-source.prototype.manual" },
                beliefId = "belief.prototype.recipe",
                memoryId = "memory.prototype.recipe"
            });

            RecipeResolvedSnapshot projection = knowledge.ProjectKnownRecipe(truth, record, RecipeProjectionAccessLevel.Ordinary);

            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Inputs.Count, Is.EqualTo(1));
            Assert.That(projection.ProcedureSteps.Count, Is.EqualTo(1));
            Assert.That(truth.Inputs.Count, Is.GreaterThan(projection.Inputs.Count));
            Assert.That(record.incorrect, Is.True);
            Assert.That(record.outdated, Is.True);
        }

        [Test]
        public void RecipeKnowledgePersistenceRejectsInvalidPayloadWithoutMutatingLiveState()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeKnowledgeRuntime runtime = new RecipeKnowledgeRuntime();
            runtime.LearnOrUpdate(new RecipeKnowledgeRecordData
            {
                recordId = "recipe-knowledge.person.smith.valid",
                personId = "person.smith",
                recipeId = fixture.Recipe.Id,
                versionId = "recipe-version.prototype-sword.v1",
                completeness = RecipeKnowledgeCompleteness.Complete
            });
            RecipeKnowledgePersistenceParticipant participant = new RecipeKnowledgePersistenceParticipant(runtime, () => fixture.Registry, "person.smith");
            RecipeKnowledgeSaveData bad = new RecipeKnowledgeSaveData
            {
                records =
                {
                    new RecipeKnowledgeRecordData { recordId = "bad", personId = "person.smith", recipeId = "recipe.missing" }
                }
            };

            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(JsonUtility.ToJson(bad), RecipeKnowledgePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepare.Succeeded, Is.False);
            Assert.That(runtime.RecordCount, Is.EqualTo(1));
            Assert.That(runtime.QueryPerson("person.smith").Single().recipeId, Is.EqualTo(fixture.Recipe.Id));
        }

        [Test]
        public void RecipeSnapshotsAndKnowledgeSavesAreImmutableCopies()
        {
            RecipeFixture fixture = CreateFixture();
            RecipeKnowledgeRuntime knowledge = new RecipeKnowledgeRuntime();
            knowledge.LearnOrUpdate(new RecipeKnowledgeRecordData
            {
                recordId = "recipe-knowledge.person.smith.immutable",
                personId = "person.smith",
                recipeId = fixture.Recipe.Id,
                knownInputIds = new[] { "recipe-input.iron" }
            });

            RecipeKnowledgeSaveData save = knowledge.CreateSaveData();
            save.records[0].recipeId = "recipe.mutated";
            save.records[0].knownInputIds[0] = "recipe-input.mutated";

            RecipeKnowledgeRecordData current = knowledge.QueryPerson("person.smith").Single();
            Assert.That(current.recipeId, Is.EqualTo(fixture.Recipe.Id));
            Assert.That(current.knownInputIds.Single(), Is.EqualTo("recipe-input.iron"));
        }

        private static RecipeFixture CreateFixture()
        {
            ItemDefinition sword = Item("item.prototype-recipe-sword");
            MaterialDefinition iron = Material("material.prototype.recipe-iron", MaterialCategory.Metal);
            MaterialDefinition catalyst = Material("material.prototype.secret-catalyst", MaterialCategory.Mineral);
            ProductionRequirementRuntime production = new ProductionRequirementRuntime();
            RecipeDefinition recipe = Recipe(sword, iron, catalyst);
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { sword, iron, catalyst, recipe });
            return new RecipeFixture(sword, iron, catalyst, recipe, registry, new ItemInstanceIdentityRuntime(), new ItemDurabilityRuntime(), production);
        }

        private static RecipeDefinition Recipe(ItemDefinition sword, MaterialDefinition iron, MaterialDefinition catalyst)
        {
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            SetField(recipe, "recipeId", "recipe.prototype.sword");
            SetField(recipe, "displayName", "Prototype Sword Recipe");
            SetField(recipe, "category", RecipeCategory.Smithing);
            SetField(recipe, "currentVersionId", "recipe-version.prototype-sword.v1");
            SetField(recipe, "versions", new[]
            {
                new RecipeVersionData { versionId = "recipe-version.prototype-sword.v0", versionLabel = "Old", state = RecipeLifecycleState.Deprecated },
                new RecipeVersionData { versionId = "recipe-version.prototype-sword.v1", versionLabel = "Current", priorVersionId = "recipe-version.prototype-sword.v0" }
            });
            SetField(recipe, "variants", new[]
            {
                new RecipeVariantData
                {
                    variantId = "recipe-variant.prototype.decorated",
                    baseVersionId = "recipe-version.prototype-sword.v1",
                    additionalInputs = new[] { Input("recipe-input.optional-trim", RecipeInputRole.DecorativeComponent, iron.Id, 0.25f, false, RecipeRequirementState.Optional) }
                }
            });
            SetField(recipe, "inputs", new[]
            {
                Input("recipe-input.iron", RecipeInputRole.PrimaryMaterial, iron.Id, 2f, false, RecipeRequirementState.Required),
                Input("recipe-input.secret-catalyst", RecipeInputRole.Catalyst, catalyst.Id, 1f, true, RecipeRequirementState.Required)
            });
            SetField(recipe, "outputs", new[]
            {
                new RecipeOutputSpecificationData { outputId = "recipe-output.sword", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = sword.Id, quantity = 1f },
                new RecipeOutputSpecificationData { outputId = "recipe-output.scrap", role = RecipeOutputRole.Scrap, materialDefinitionId = iron.Id, quantity = 0.1f, conditional = true }
            });
            SetField(recipe, "procedureSteps", new[]
            {
                Step("recipe-step.prepare", RecipeProcedureStepKind.PrepareInput),
                Step("recipe-step.shape", RecipeProcedureStepKind.Shape, "recipe-step.prepare"),
                Step("recipe-step.finish", RecipeProcedureStepKind.Finish, "recipe-step.shape")
            });
            SetField(recipe, "transferMappings", new[]
            {
                new RecipeTransferMappingData { mappingId = "recipe-transfer.iron-to-sword", sourceInputId = "recipe-input.iron", targetOutputId = "recipe-output.sword", quantityTransferPolicy = RecipeTransferPolicy.InputDerived }
            });
            SetField(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Discrete, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 5f, batchIncrement = 1f });
            SetField(recipe, "compositionTransferPolicyId", "recipe-policy.composition.inherit-primary-material");
            SetField(recipe, "qualityGenerationPolicyId", "recipe-policy.quality.smithing");
            SetField(recipe, "affixGenerationPolicyId", "recipe-policy.affix.none");
            SetField(recipe, "durabilityInitializationPolicyId", "recipe-policy.durability.newly-crafted");
            return recipe;
        }

        private static RecipeInputSpecificationData Input(string id, RecipeInputRole role, string materialId, float quantity, bool hidden, RecipeRequirementState state)
        {
            return new RecipeInputSpecificationData
            {
                inputId = id,
                role = role,
                materialDefinitionId = materialId,
                quantity = quantity,
                unit = ProductionQuantityUnit.Kilogram,
                hidden = hidden,
                requirementState = state,
                classification = role == RecipeInputRole.Catalyst ? RecipeInputClassification.Catalyst : RecipeInputClassification.Consumable
            };
        }

        private static RecipeProcedureStepData Step(string id, RecipeProcedureStepKind kind, params string[] dependencies)
        {
            return new RecipeProcedureStepData
            {
                stepId = id,
                stepKind = kind,
                displayName = id,
                dependsOnStepIds = dependencies ?? Array.Empty<string>()
            };
        }

        private static ItemDefinition Item(string id)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "itemId", id);
            SetField(item, "displayName", id);
            SetField(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            SetField(item, "stackable", false);
            return item;
        }

        private static MaterialDefinition Material(string id, MaterialCategory category)
        {
            MaterialDefinition material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetField(material, "materialId", id);
            SetField(material, "displayName", id);
            SetField(material, "category", category);
            return material;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private sealed class RecipeFixture
        {
            public RecipeFixture(ItemDefinition sword, MaterialDefinition iron, MaterialDefinition secretCatalyst, RecipeDefinition recipe, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemDurabilityRuntime durability, ProductionRequirementRuntime production)
            {
                Sword = sword;
                Iron = iron;
                SecretCatalyst = secretCatalyst;
                Recipe = recipe;
                Registry = registry;
                Items = items;
                Durability = durability;
                Production = production;
                Definitions = new IGameDefinition[] { sword, iron, secretCatalyst, recipe };
            }

            public ItemDefinition Sword { get; }
            public MaterialDefinition Iron { get; }
            public MaterialDefinition SecretCatalyst { get; }
            public RecipeDefinition Recipe { get; }
            public DefinitionRegistry Registry { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ItemDurabilityRuntime Durability { get; }
            public ProductionRequirementRuntime Production { get; }
            public IGameDefinition[] Definitions { get; }
        }
    }
}
