#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Integration;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(9, "Items", 900)]
    public static class PrototypeStep9AutomationSuites
    {
        private const string SwordId = "item.prototype-sword";
        private const string PotionId = "item.health-potion";

        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, BuildItemIdentitySuite());
            TryRegister(registry, BuildMaterialsCompositionSuite());
            TryRegister(registry, BuildQualityAffixSuite());
            TryRegister(registry, BuildDurabilitySuite());
            TryRegister(registry, BuildProductionRequirementsSuite());
            TryRegister(registry, BuildRecipesCraftingKnowledgeSuite());
            TryRegister(registry, BuildCraftingExecutionSuite());
            TryRegister(registry, BuildProductionWorkflowSuite());
            TryRegister(registry, BuildExperimentationDiscoverySuite());
            TryRegister(registry, BuildItemCraftingIntegrationFinalizationSuite());
        }

        private static ITestLabAutomationSuite BuildItemIdentitySuite()
        {
            return Suite("feature.9.1.item-identity-instance-state", "Feature 9.1 Item Identity and Instance State", "9.1", 910,
                Required("ItemInstanceIdentityRuntime", "ItemIdentityInventoryBridge", "PlayerItemIdentitySynchronizer", "ItemInstanceRecordData", "ItemInstanceRuntimeSaveData"),
                Scenario("distinct-instances", "Two items sharing one definition keep distinct identities", 10,
                    Step("step9-items-distinct", "Create distinct item instances", DistinctInstances)),
                Scenario("ownership-custody", "Ownership and custody mutate independently", 20,
                    Step("step9-items-ownership", "Transfer ownership and custody", OwnershipCustody)),
                Scenario("location-validation", "Location validation rejects duplicate world placement", 30,
                    Step("step9-items-location", "Validate world placement identity", LocationValidation)),
                Scenario("persistence-round-trip", "Item identity persistence round-trips state", 40,
                    Step("step9-items-persistence", "Save and restore item identity", PersistenceRoundTrip)),
                Scenario("inventory-equipment-migration", "Legacy inventory and equipment saves migrate into item identity", 50,
                    Step("step9-items-migration", "Migrate inventory and equipment graph", InventoryEquipmentMigration)),
                Scenario("inventory-equipment-synchronization", "Current inventory and equipment projection synchronizes item identity", 55,
                    Step("step9-items-sync", "Synchronize inventory and equipment graph", InventoryEquipmentSynchronization)),
                Scenario("access-subject", "Item projections expose stable Step 8 subject references", 60,
                    Step("step9-items-access", "Project item information subject", AccessSubject)));
        }

        private static ITestLabAutomationSuite BuildMaterialsCompositionSuite()
        {
            return Suite("feature.9.2.materials-item-composition", "Feature 9.2 Materials and Item Composition", "9.2", 920,
                Required("ItemCompositionRuntime", "MaterialDefinition", "MaterialCompatibilityRuleDefinition", "ItemInstanceIdentityRuntime"),
                Scenario("material-definition-runtime", "Materials and composite definitions resolve deterministically", 10,
                    Step("step9-materials-resolve", "Resolve material definitions", MaterialDefinitionsResolve)),
                Scenario("composition-graph-validation", "Item composition validates item, material, and component graphs", 20,
                    Step("step9-composition-graph", "Validate composition graph", CompositionGraphValidation)),
                Scenario("default-composition", "Item creation can resolve default or unknown composition", 25,
                    Step("step9-composition-default", "Ensure default composition", DefaultComposition)),
                Scenario("atomic-creation", "Required item composition commits atomically with item identity", 28,
                    Step("step9-composition-atomic", "Create required composition atomically", AtomicCompositionCreation)),
                Scenario("derived-properties", "Derived physical properties and compatibility remain deterministic", 30,
                    Step("step9-composition-properties", "Compute derived properties", DerivedProperties)),
                Scenario("tracked-components", "Tracked components require reserved item ownership", 35,
                    Step("step9-composition-tracked", "Attach tracked component", TrackedComponents)),
                Scenario("composite-expansion", "Composite material expansion is deterministic", 38,
                    Step("step9-composition-composite", "Expand composite material", CompositeExpansion)),
                Scenario("stack-equivalence", "Composition differences block stack equivalence", 40,
                    Step("step9-composition-stack", "Evaluate composition stack equivalence", StackEquivalence)),
                Scenario("projection-and-persistence", "Composition projections and persistence are atomic", 50,
                    Step("step9-composition-project-save", "Project and persist composition", ProjectionAndPersistence)));
        }

        private static ITestLabAutomationSuite BuildQualityAffixSuite()
        {
            return Suite("feature.9.3.item-quality-affixes", "Feature 9.3 Item Quality and Affixes", "9.3", 930,
                Required("ItemQualityAffixRuntime", "QualityTierDefinition", "ItemAffixDefinition", "ItemCompositionRuntime", "ItemInstanceIdentityRuntime"),
                Scenario("quality-workmanship", "Workmanship and quality tiers are authoritative and deterministic", 10,
                    Step("step9-quality-workmanship", "Create default and masterwork quality", QualityWorkmanship)),
                Scenario("defects-and-redaction", "Visible and hidden defects project through access-aware views", 20,
                    Step("step9-quality-defects", "Add defects and redact hidden entries", DefectsAndRedaction)),
                Scenario("affix-generation-deterministic", "Affix preview and execution are deterministic by seed", 30,
                    Step("step9-quality-generation", "Generate deterministic affixes", AffixGenerationDeterministic)),
                Scenario("affix-conflict-and-stack", "Conflicts and quality differences block unsafe merges", 40,
                    Step("step9-quality-conflict-stack", "Reject conflict and compare stack signatures", AffixConflictAndStack)),
                Scenario("modifier-contribution", "Equipped affix modifiers apply and remove exactly once", 50,
                    Step("step9-quality-modifiers", "Apply source-safe affix modifier", AffixModifierContribution)),
                Scenario("persistence-and-migration", "Feature 9.1/9.2 items migrate without rerolling affixes", 60,
                    Step("step9-quality-persistence", "Save and restore quality and affixes", QualityPersistenceAndMigration)));
        }

        private static ITestLabAutomationSuite BuildDurabilitySuite()
        {
            return Suite("feature.9.4.durability-wear-repair-salvage", "Feature 9.4 Durability Wear Repair and Salvage", "9.4", 940,
                Required("ItemDurabilityRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime"),
                Scenario("condition-migration-defaults", "Identity condition migrates into authoritative durability", 10,
                    Step("step9-durability-migration", "Migrate legacy condition", DurabilityMigration)),
                Scenario("damage-and-components", "Damage applies to items and components deterministically", 20,
                    Step("step9-durability-damage", "Apply component damage", DurabilityDamage)),
                Scenario("repair-capacity-loss", "Repair restores durability with permanent capacity loss", 30,
                    Step("step9-durability-repair", "Repair damaged item", DurabilityRepair)),
                Scenario("salvage-and-persistence", "Salvage outputs and persistence remain deterministic", 40,
                    Step("step9-durability-salvage-save", "Salvage and restore durability", DurabilitySalvagePersistence)),
                Scenario("projection-and-stack", "Durability projections redact details and block unsafe stacks", 50,
                    Step("step9-durability-project-stack", "Project and compare stack signatures", DurabilityProjectionStack)),
                Scenario("broken-equipment-contribution", "Broken equipped items do not contribute stats", 60,
                    Step("step9-durability-equipment", "Gate equipment stat contribution", DurabilityEquipmentContribution)));
        }

        private static ITestLabAutomationSuite BuildProductionRequirementsSuite()
        {
            return Suite("feature.9.5.tools-production-requirements", "Feature 9.5 Tools and Production Requirements", "9.5", 950,
                Required("ProductionRequirementRuntime", "ProductionToolDefinition", "ProductionStationDefinition", "ProductionRequirementDefinition", "ItemDurabilityRuntime"),
                Scenario("tool-station-selection", "Exact tools, substitutes, and stations produce deterministic plans", 10,
                    Step("step9-production-selection", "Select production tools and station", ProductionToolStationSelection)),
                Scenario("resource-skill-knowledge-requirements", "Production plans include non-tool requirements", 20,
                    Step("step9-production-requirements", "Evaluate resource skill and knowledge requirements", ProductionResourceSkillKnowledge)),
                Scenario("reservations-and-invalidation", "Reservations block conflicts and dependency changes invalidate plans", 30,
                    Step("step9-production-reservations", "Reserve and invalidate production plans", ProductionReservationsInvalidation)),
                Scenario("wear-and-persistence", "Tool wear and persistence preserve production plans", 40,
                    Step("step9-production-wear-save", "Apply tool wear and restore production runtime", ProductionWearPersistence)));
        }

        private static ITestLabAutomationSuite BuildRecipesCraftingKnowledgeSuite()
        {
            return Suite("feature.9.6.recipes-crafting-knowledge", "Feature 9.6 Recipes and Crafting Knowledge", "9.6", 960,
                Required("RecipeDefinition", "RecipeRuntime", "RecipeKnowledgeRuntime", "ProductionRequirementRuntime"),
                Scenario("definition-resolution", "Recipes validate versions, variants, inputs, outputs, and procedures", 10,
                    Step("step9-recipes-definition", "Validate recipe definition", RecipeDefinitionResolution)),
                Scenario("preview-and-reservation", "Recipe preview is read-only and reservation is explicit", 20,
                    Step("step9-recipes-preview", "Preview and reserve recipe", RecipePreviewReservation)),
                Scenario("knowledge-projection", "Person recipe knowledge projects partial and privileged views", 30,
                    Step("step9-recipes-knowledge", "Project recipe knowledge", RecipeKnowledgeProjection)),
                Scenario("persistence-round-trip", "Recipe knowledge persistence validates before commit", 40,
                    Step("step9-recipes-persistence", "Persist recipe knowledge", RecipeKnowledgePersistence)));
        }

        private static ITestLabAutomationSuite BuildCraftingExecutionSuite()
        {
            return Suite("feature.9.7.crafting-execution", "Feature 9.7 Crafting Execution", "9.7", 970,
                Required("CraftingExecutionRuntime", "RecipeRuntime", "ProductionRequirementRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime", "ItemDurabilityRuntime"),
                Scenario("preview-is-readonly", "Crafting execution preview resolves without mutating owned runtimes", 10,
                    Step("step9-crafting-preview", "Preview crafting execution", CraftingPreviewReadonly)),
                Scenario("execute-produces-output-graph", "Crafting execution consumes a reserved plan and creates output item state", 20,
                    Step("step9-crafting-execute", "Execute crafting operation", CraftingExecuteOutputGraph)),
                Scenario("duplicate-operation-idempotent", "Duplicate crafting operation returns committed result without duplicate output", 30,
                    Step("step9-crafting-duplicate", "Replay crafting operation", CraftingDuplicateIdempotent)),
                Scenario("failure-rolls-back", "Crafting execution rolls back partial downstream mutations on failure", 40,
                    Step("step9-crafting-rollback", "Rollback failed crafting operation", CraftingFailureRollback)),
                Scenario("persistence-round-trip", "Crafting execution persistence preserves completed operations without replay", 50,
                    Step("step9-crafting-persistence", "Persist crafting execution", CraftingPersistence)));
        }

        private static ITestLabAutomationSuite BuildProductionWorkflowSuite()
        {
            return Suite("feature.9.8.production-chains-batch-work", "Feature 9.8 Production Chains and Batch Work", "9.8", 980,
                Required("ProductionWorkflowRuntime", "ProductionChainDefinition", "ProductionRequirementRuntime", "CraftingExecutionRuntime", "ItemInstanceIdentityRuntime"),
                Scenario("chain-validation", "Production chain stage graph validation catches cycles and preserves snapshots", 10,
                    Step("step9-production-chain-validate", "Validate production chain graph", ProductionChainValidation)),
                Scenario("work-order-job-queue", "Work orders create stable queued production jobs", 20,
                    Step("step9-production-work-order", "Create queued work order and job", ProductionWorkOrderJobQueue)),
                Scenario("progress-idempotent", "Explicit world-time progression is deterministic and idempotent", 30,
                    Step("step9-production-progress", "Advance production by world time", ProductionProgressIdempotent)),
                Scenario("stage-output-lineage", "Stage completion creates batch, lot, intermediate, and output lineage once", 40,
                    Step("step9-production-lineage", "Complete production stage lineage", ProductionStageOutputLineage)),
                Scenario("pause-interrupt-recover-cancel", "Pause, resume, interruption, recovery, and cancellation preserve boundaries", 50,
                    Step("step9-production-lifecycle", "Exercise production lifecycle boundaries", ProductionLifecycleBoundaries)),
                Scenario("persistence-and-projection", "Production workflow persistence and projections preserve access-safe state", 60,
                    Step("step9-production-save-project", "Persist and project production workflow", ProductionPersistenceProjection)));
        }

        private static ITestLabAutomationSuite BuildExperimentationDiscoverySuite()
        {
            return Suite("feature.9.9.experimentation-discovery", "Feature 9.9 Experimentation and Discovery", "9.9", 990,
                Required("ExperimentationRuntime", "ExperimentDefinition", "PersonKnowledgeRuntime", "CraftingExecutionRuntime", "ProductionWorkflowRuntime"),
                Scenario("definitions-and-hypotheses", "Experiment definitions and hypotheses remain typed", 10,
                    Step("step9-experiment-hypothesis", "Create experiment hypothesis", ExperimentHypothesisFoundation)),
                Scenario("controlled-plan-preview", "Controlled experiment plans preview without mutating runtime state", 20,
                    Step("step9-experiment-plan-preview", "Preview controlled experiment plan", ExperimentControlledPlanPreview)),
                Scenario("crafting-trial-evidence", "Crafting trial output creates explicit evidence and inference", 30,
                    Step("step9-experiment-crafting-evidence", "Run crafting trial and record evidence", ExperimentCraftingEvidence)),
                Scenario("reproducibility-claim-registration", "Reproduction confirms claims without mutating authoritative recipes", 40,
                    Step("step9-experiment-claim-registration", "Confirm discovery claim and propose registration", ExperimentClaimRegistration)),
                Scenario("access-and-persistence", "Experiment projections and save restore preserve access-safe state", 50,
                    Step("step9-experiment-access-persistence", "Project and persist experimentation", ExperimentAccessPersistence)));
        }

        private static ITestLabAutomationSuite BuildItemCraftingIntegrationFinalizationSuite()
        {
            return Suite("feature.9.10.item-crafting-integration-finalization", "Feature 9.10 Item and Crafting Integration Finalization", "9.10", 1000,
                Required("Step9IntegrationValidator", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime", "ItemDurabilityRuntime", "ProductionRequirementRuntime", "RecipeKnowledgeRuntime", "CraftingExecutionRuntime", "ProductionWorkflowRuntime", "ExperimentationRuntime"),
                Scenario("readiness-and-validation", "Integrated Step 9 readiness, authority, and graph validation pass", 10,
                    Step("step9-integration-validate", "Validate Step 9 integrated runtime graph", Step9IntegrationReadinessValidation)),
                Scenario("conflict-detection-and-fingerprint", "Integration validator catches graph conflicts and keeps deterministic fingerprints", 20,
                    Step("step9-integration-conflicts", "Detect Step 9 graph conflicts", Step9IntegrationConflictDetection)));
        }

        private static TestLabAutomationStepResult Step9IntegrationReadinessValidation(TestLabAutomationContext context)
        {
            Step9IntegrationRuntimeSnapshot snapshot = Step9Snapshot(context);
            Step9IntegrationValidationReport report = Step9IntegrationValidator.ValidateRuntimeGraph(snapshot, context.ScenarioContext.Runtimes.DefinitionRegistry);
            string fingerprint = Step9IntegrationValidator.CreateCanonicalFingerprint(snapshot);

            return report.Succeeded
                ? Pass(context, "step9-integration-validate", $"Readiness=True {report.ToSummary()} Fingerprint={fingerprint}")
                : Fail(context, "step9-integration-validate", $"Readiness=False {report.ToSummary()} Diagnostics={string.Join(" | ", report.Diagnostics.Select(diagnostic => diagnostic.ToString()))}");
        }

        private static TestLabAutomationStepResult Step9IntegrationConflictDetection(TestLabAutomationContext context)
        {
            Step9IntegrationRuntimeSnapshot baseline = Step9Snapshot(context);
            ItemInstanceRuntimeSaveData items = baseline.ItemInstances.Clone();
            items.records.Add(new ItemInstanceRecordData
            {
                itemInstanceId = "test.integration.item.a",
                itemDefinitionId = SwordId,
                lifecycleState = ItemLifecycleState.Active,
                location = new ItemLocationStateData { kind = ItemLocationKind.Equipped, equipmentHolderId = "person.prototype", equipmentSlotId = "main-hand" }
            });
            items.records.Add(new ItemInstanceRecordData
            {
                itemInstanceId = "test.integration.item.b",
                itemDefinitionId = SwordId,
                lifecycleState = ItemLifecycleState.Active,
                location = new ItemLocationStateData { kind = ItemLocationKind.Equipped, equipmentHolderId = "person.prototype", equipmentSlotId = "main-hand" }
            });

            Step9IntegrationRuntimeSnapshot invalid = new Step9IntegrationRuntimeSnapshot(
                itemInstances: items,
                itemCompositions: baseline.ItemCompositions,
                itemQualityAffixes: baseline.ItemQualityAffixes,
                itemDurability: baseline.ItemDurability,
                productionRequirements: baseline.ProductionRequirements,
                recipeKnowledge: baseline.RecipeKnowledge,
                craftingExecution: baseline.CraftingExecution,
                productionWorkflow: baseline.ProductionWorkflow,
                experimentation: baseline.Experimentation);
            Step9IntegrationValidationReport invalidReport = Step9IntegrationValidator.ValidateRuntimeGraph(invalid, context.ScenarioContext.Runtimes.DefinitionRegistry);
            bool conflictFound = invalidReport.Diagnostics.Any(diagnostic => diagnostic.Code == "DuplicateExclusiveLocation");
            bool baselineUnchanged = Step9IntegrationValidator.CreateCanonicalFingerprint(baseline) == Step9IntegrationValidator.CreateCanonicalFingerprint(Step9Snapshot(context));

            return !invalidReport.Succeeded && conflictFound && baselineUnchanged
                ? Pass(context, "step9-integration-conflicts", $"ConflictFound=True BaselineUnchanged=True Errors={invalidReport.ErrorCount}")
                : Fail(context, "step9-integration-conflicts", $"ConflictFound={conflictFound} BaselineUnchanged={baselineUnchanged} {invalidReport.ToSummary()} Diagnostics={string.Join(" | ", invalidReport.Diagnostics.Select(diagnostic => diagnostic.ToString()))}");
        }

        private static Step9IntegrationRuntimeSnapshot Step9Snapshot(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            return new Step9IntegrationRuntimeSnapshot(
                runtimes.ItemInstances?.CreateSaveData(),
                runtimes.ItemCompositions?.CreateSaveData(),
                runtimes.ItemQualityAffixes?.CreateSaveData(),
                runtimes.ItemDurability?.CreateSaveData(),
                runtimes.ProductionRequirements?.CreateSaveData(),
                runtimes.RecipeKnowledge?.CreateSaveData(),
                runtimes.CraftingExecution?.CreateSaveData(),
                runtimes.ProductionWorkflow?.CreateSaveData(),
                runtimes.Experimentation?.CreateSaveData());
        }

        private static TestLabAutomationStepResult ExperimentHypothesisFoundation(TestLabAutomationContext context)
        {
            if (!TryExperimentationFixture(context, out ExperimentationRuntime experimentation, out DefinitionRegistry registry, out ExperimentDefinition experiment, out _, out string failure))
            {
                return Fail(context, "step9-experiment-hypothesis", failure);
            }

            ExperimentHypothesisData hypothesis = PrototypeHypothesis(context, "foundation");
            ExperimentationResult create = experimentation.CreateHypothesis(hypothesis);
            ExperimentationResult duplicate = experimentation.CreateHypothesis(hypothesis);
            bool valid = create.Succeeded
                && duplicate.Duplicate
                && create.Hypothesis.claim.claimType == HypothesisClaimType.MaterialSubstitutesForMaterial
                && registry.TryGet(experiment.Id, out ExperimentDefinition _)
                && experimentation.HypothesisCount == 1;
            return valid
                ? Pass(context, "step9-experiment-hypothesis", $"Hypothesis={create.Hypothesis.hypothesisId} Status={create.Hypothesis.status} Duplicate={duplicate.Duplicate}")
                : Fail(context, "step9-experiment-hypothesis", $"Create={create.Status} Duplicate={duplicate.Status} Count={experimentation.HypothesisCount}");
        }

        private static TestLabAutomationStepResult ExperimentControlledPlanPreview(TestLabAutomationContext context)
        {
            if (!TryExperimentationFixture(context, out ExperimentationRuntime experimentation, out DefinitionRegistry registry, out ExperimentDefinition experiment, out _, out string failure))
            {
                return Fail(context, "step9-experiment-plan-preview", failure);
            }

            ExperimentationResult hypothesis = experimentation.CreateHypothesis(PrototypeHypothesis(context, "plan"));
            long before = experimentation.Revision;
            ExperimentPlanData plan = PrototypeExperimentPlan(context, experiment.Id, hypothesis.Hypothesis.hypothesisId);
            ExperimentationResult preview = experimentation.CreatePlan(plan, registry, preview: true);
            bool previewReadOnly = preview.Preview && experimentation.PlanCount == 0 && experimentation.Revision == before;
            ExperimentationResult create = experimentation.CreatePlan(plan, registry);
            bool valid = previewReadOnly && create.Succeeded && experimentation.PlanCount == 1;
            return valid
                ? Pass(context, "step9-experiment-plan-preview", $"Preview={preview.Status} Create={create.Status} Revision={before}->{experimentation.Revision}")
                : Fail(context, "step9-experiment-plan-preview", $"Preview={preview.Status} Create={create.Status} Plans={experimentation.PlanCount} Revision={before}->{experimentation.Revision}");
        }

        private static TestLabAutomationStepResult ExperimentCraftingEvidence(TestLabAutomationContext context)
        {
            if (!TryExperimentationFixture(context, out ExperimentationRuntime experimentation, out DefinitionRegistry registry, out ExperimentDefinition experiment, out RecipeDefinition recipe, out string failure))
            {
                return Fail(context, "step9-experiment-crafting-evidence", failure);
            }

            ExperimentHypothesisData hypothesis = PrototypeHypothesis(context, "evidence");
            ExperimentationResult createdHypothesis = experimentation.CreateHypothesis(hypothesis);
            ExperimentationResult createdPlan = experimentation.CreatePlan(PrototypeExperimentPlan(context, experiment.Id, createdHypothesis.Hypothesis.hypothesisId, recipe.Id), registry);
            string runId = context.ScenarioContext.ScopedId("experiment-run", "evidence");
            ExperimentationResult started = experimentation.StartRun(runId, createdPlan.Plan.planId, context.ScenarioContext.Runtimes.PersonId, "1", registry);
            MaterialDefinition iron = registry.DefinitionsById.Values.OfType<MaterialDefinition>().First(material => material.Id == "material.prototype.recipe-iron");
            CraftingExecutionRequest request = CraftingRequest(context, recipe, iron, "experiment");
            ExperimentationResult trial = experimentation.ExecuteCraftingTrial(
                runId,
                context.ScenarioContext.ScopedId("experiment-trial", "crafting"),
                request,
                registry,
                new RecipeRuntime(),
                context.ScenarioContext.Runtimes.ProductionRequirements,
                context.ScenarioContext.Runtimes.ItemInstances,
                context.ScenarioContext.Runtimes.ItemCompositions,
                context.ScenarioContext.Runtimes.ItemQualityAffixes,
                context.ScenarioContext.Runtimes.ItemDurability,
                context.ScenarioContext.Runtimes.CraftingExecution);

            context.ScenarioContext.Runtimes.Knowledge.Configure(registry, context.ScenarioContext.Runtimes.PersonId);
            ExperimentationResult evidence = experimentation.GenerateEvidence(
                runId,
                trial.Trial?.trialId,
                createdHypothesis.Hypothesis.hypothesisId,
                ExperimentObservation(context, "evidence", recipe.Id),
                context.ScenarioContext.Runtimes.Knowledge,
                ExperimentEvidenceRole.Supporting);
            ExperimentationResult inference = experimentation.RecordInference(new ExperimentInferenceData
            {
                inferenceId = context.ScenarioContext.ScopedId("experiment-inference", "recipe-fragment"),
                experimentRunId = runId,
                inferenceType = ExperimentInferenceType.RecipeFragment,
                subjectId = recipe.Id,
                inferredDefinitionId = "recipe-input.prototype.iron",
                evidenceIds = evidence.Run?.evidenceIds ?? Array.Empty<string>(),
                confidence = 720,
                message = "Observed output supports iron as a recipe fragment."
            });

            bool valid = createdPlan.Succeeded
                && started.Succeeded
                && trial.Succeeded
                && evidence.Succeeded
                && inference.Succeeded
                && context.ScenarioContext.Runtimes.Knowledge.CreateSnapshot(KnowledgeDomain.Crafting).Evidence.Count > 0;
            return valid
                ? Pass(context, "step9-experiment-crafting-evidence", $"Trial={trial.Trial.outcome} Evidence={evidence.Run.evidenceIds.Length} Inference={inference.Inference.inferenceType}")
                : Fail(context, "step9-experiment-crafting-evidence", $"Plan={createdPlan.Status} Run={started.Status} Trial={trial.Status} Evidence={evidence.Status} Inference={inference.Status}");
        }

        private static TestLabAutomationStepResult ExperimentClaimRegistration(TestLabAutomationContext context)
        {
            if (!TryExperimentationFixture(context, out ExperimentationRuntime experimentation, out DefinitionRegistry registry, out ExperimentDefinition experiment, out RecipeDefinition recipe, out string failure))
            {
                return Fail(context, "step9-experiment-claim-registration", failure);
            }

            ExperimentationResult hypothesis = experimentation.CreateHypothesis(PrototypeHypothesis(context, "claim"));
            ExperimentationResult plan = experimentation.CreatePlan(PrototypeExperimentPlan(context, experiment.Id, hypothesis.Hypothesis.hypothesisId, recipe.Id), registry);
            string runId = context.ScenarioContext.ScopedId("experiment-run", "claim");
            experimentation.StartRun(runId, plan.Plan.planId, context.ScenarioContext.Runtimes.PersonId, "1", registry);
            for (int i = 0; i < 2; i++)
            {
                experimentation.RecordTrial(new ExperimentTrialData
                {
                    trialId = context.ScenarioContext.ScopedId("experiment-trial", $"repro-{i}"),
                    experimentRunId = runId,
                    trialIndex = i,
                    trialKind = i == 0 ? ExperimentTrialKind.Experimental : ExperimentTrialKind.Reproduction,
                    recipeDefinitionId = recipe.Id,
                    recipeVersionId = recipe.CurrentVersionId,
                    deterministicSeed = $"seed.repro.{i}",
                    outcome = ExperimentTrialOutcome.ExpectedSuccess
                });
            }

            ExperimentationResult inference = experimentation.RecordInference(new ExperimentInferenceData
            {
                inferenceId = context.ScenarioContext.ScopedId("experiment-inference", "claim"),
                experimentRunId = runId,
                inferenceType = ExperimentInferenceType.Substitution,
                subjectId = recipe.Id,
                inferredDefinitionId = "material.prototype.recipe-iron",
                confidence = 800
            });
            ExperimentationResult claim = experimentation.CreateDiscoveryClaim(new DiscoveryClaimData
            {
                claimId = context.ScenarioContext.ScopedId("discovery-claim", "claim"),
                experimentRunId = runId,
                inferenceId = inference.Inference.inferenceId,
                hypothesisId = hypothesis.Hypothesis.hypothesisId,
                evidenceIds = new[] { "evidence.prototype.reproduction-a", "evidence.prototype.reproduction-b" },
                supportCount = 2,
                confidence = 850
            }, experiment.ConfirmationPolicy);
            ExperimentationResult review = experimentation.ReviewDiscoveryClaim(new DiscoveryReviewData
            {
                reviewId = context.ScenarioContext.ScopedId("discovery-review", "claim"),
                claimId = claim.Claim.claimId,
                reviewerPersonId = context.ScenarioContext.Runtimes.PersonId,
                decision = DiscoveryReviewDecision.Confirm,
                reason = "Independent reproduction threshold met."
            });
            int recipeCountBefore = registry.DefinitionsById.Values.OfType<RecipeDefinition>().Count();
            ExperimentationResult proposal = experimentation.ProposeRecipeRegistration(new RecipeRegistrationProposalData
            {
                proposalId = context.ScenarioContext.ScopedId("recipe-registration-proposal", "claim"),
                claimId = claim.Claim.claimId,
                proposedRecipeId = "recipe.prototype.discovered-variant",
                requesterPersonId = context.ScenarioContext.Runtimes.PersonId,
                authorizationId = "authorization.prototype.discovery-review",
                authorized = true
            });
            int recipeCountAfter = registry.DefinitionsById.Values.OfType<RecipeDefinition>().Count();

            bool valid = claim.Succeeded
                && review.Succeeded
                && proposal.Succeeded
                && claim.Claim.status == DiscoveryClaimStatus.Confirmed
                && proposal.RegistrationProposal.submitted
                && recipeCountBefore == recipeCountAfter;
            return valid
                ? Pass(context, "step9-experiment-claim-registration", $"Claim={claim.Claim.status} Proposal={proposal.RegistrationProposal.proposalId} Recipes={recipeCountBefore}->{recipeCountAfter}")
                : Fail(context, "step9-experiment-claim-registration", $"Claim={claim.Status}/{claim.Claim?.status} Review={review.Status} Proposal={proposal.Status} Recipes={recipeCountBefore}->{recipeCountAfter}");
        }

        private static TestLabAutomationStepResult ExperimentAccessPersistence(TestLabAutomationContext context)
        {
            if (!TryExperimentationFixture(context, out ExperimentationRuntime experimentation, out DefinitionRegistry registry, out ExperimentDefinition experiment, out RecipeDefinition recipe, out string failure))
            {
                return Fail(context, "step9-experiment-access-persistence", failure);
            }

            ExperimentationResult hypothesis = experimentation.CreateHypothesis(PrototypeHypothesis(context, "access"));
            ExperimentationResult plan = experimentation.CreatePlan(PrototypeExperimentPlan(context, experiment.Id, hypothesis.Hypothesis.hypothesisId, recipe.Id), registry);
            string runId = context.ScenarioContext.ScopedId("experiment-run", "access");
            experimentation.StartRun(runId, plan.Plan.planId, context.ScenarioContext.Runtimes.PersonId, "1", registry);
            experimentation.RecordTrial(new ExperimentTrialData
            {
                trialId = context.ScenarioContext.ScopedId("experiment-trial", "access"),
                experimentRunId = runId,
                trialIndex = 0,
                recipeDefinitionId = recipe.Id,
                outcome = ExperimentTrialOutcome.ExpectedSuccess,
                deterministicSeed = "seed.access"
            });

            string policyId = context.ScenarioContext.ScopedId("information-access-policy", "experiment");
            context.ScenarioContext.Runtimes.Access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = ExperimentInformationSubject.Experiment(runId, experiment.Id),
                classification = InformationVisibilityClassification.Confidential,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Redacted,
                allowedPersonIds = new[] { "person.prototype.observer" },
                defaultVisibleDetails = new[] { "detail.run" },
                defaultRedactedDetails = new[] { "detail.trials", "detail.evidence", "detail.inputs", "detail.outputs", "detail.provenance" }
            });
            ExperimentProjectionData projection = experimentation.ProjectRun(runId, context.ScenarioContext.Runtimes.Access, new InformationAccessContext
            {
                RequestingPersonId = "person.prototype.observer",
                RedactedAccessAcceptable = true,
                RevealDenialReasons = true
            }, policyId);

            ExperimentationRuntimeSaveData save = experimentation.CreateSaveData();
            ExperimentationRuntime restored = new ExperimentationRuntime();
            ExperimentationResult restore = restored.RestoreFromSaveData(save, registry);
            ExperimentationRuntimeSaveData corrupt = save.Clone();
            corrupt.trials[0].experimentRunId = "experiment-run.missing";
            ExperimentationResult rejected = restored.RestoreFromSaveData(corrupt, registry);
            bool valid = projection.Decision == ExperimentProjectionDecision.RedactedAccess
                && restore.Succeeded
                && restored.RunCount == experimentation.RunCount
                && !rejected.Succeeded
                && restored.RunCount == experimentation.RunCount;
            return valid
                ? Pass(context, "step9-experiment-access-persistence", $"Projection={projection.Decision} Restore={restore.Status} Reject={rejected.Status}")
                : Fail(context, "step9-experiment-access-persistence", $"Projection={projection.Decision} Restore={restore.Status} Reject={rejected.Status} Counts={restored.RunCount}/{experimentation.RunCount}");
        }

        private static bool TryExperimentationFixture(
            TestLabAutomationContext context,
            out ExperimentationRuntime experimentation,
            out DefinitionRegistry registry,
            out ExperimentDefinition experiment,
            out RecipeDefinition recipe,
            out string failure)
        {
            experimentation = context?.ScenarioContext?.Runtimes?.Experimentation;
            experiment = null;
            recipe = null;
            registry = null;
            failure = string.Empty;
            if (experimentation == null)
            {
                failure = "Experimentation runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (!TryCreateCraftingRuntime(context, out _, out _, out _, out DefinitionRegistry baseRegistry, out recipe, out MaterialDefinition iron, out failure))
            {
                return false;
            }

            experiment = PrototypeExperimentDefinition();
            KnowledgeFactDefinition fact = ExperimentKnowledgeFact();
            registry = ExtendRegistry(baseRegistry, iron, recipe, experiment, fact);
            context.ScenarioContext.Runtimes.Knowledge.Configure(registry, context.ScenarioContext.Runtimes.PersonId);
            return true;
        }

        private static ExperimentDefinition PrototypeExperimentDefinition()
        {
            ExperimentDefinition definition = UnityEngine.ScriptableObject.CreateInstance<ExperimentDefinition>();
            SetPrivate(definition, "experimentId", "experiment.prototype.recipe-substitution");
            SetPrivate(definition, "displayName", "Prototype Recipe Substitution Experiment");
            SetPrivate(definition, "category", ExperimentCategory.SubstitutionTesting);
            SetPrivate(definition, "defaultPlanMode", ExperimentPlanMode.Controlled);
            SetPrivate(definition, "tags", new[] { "experiment.prototype", "experiment.recipe", "experiment.substitution" });
            SetPrivate(definition, "supportedTargetTypes", new[] { "recipe", "material", "tool", "station" });
            SetPrivate(definition, "variables", new[]
            {
                new ExperimentVariableDefinitionData
                {
                    variableId = "experiment-variable.prototype.substitute-material",
                    category = ExperimentVariableCategory.IngredientIdentity,
                    valueType = ExperimentValueType.StableId,
                    role = ExperimentVariableRole.Independent,
                    allowedValueIds = new[] { "material.prototype.recipe-iron" },
                    controlValue = "material.prototype.recipe-iron"
                },
                new ExperimentVariableDefinitionData
                {
                    variableId = "experiment-variable.prototype.output-quality",
                    category = ExperimentVariableCategory.Custom,
                    valueType = ExperimentValueType.Numeric,
                    role = ExperimentVariableRole.Dependent,
                    minimumValue = 0f,
                    maximumValue = 1000f
                }
            });
            SetPrivate(definition, "requiredControls", new[]
            {
                new ExperimentControlDefinitionData
                {
                    controlId = "experiment-control.prototype.known-iron",
                    baselineType = "known-recipe",
                    baselineReferenceId = "recipe.prototype.sword",
                    heldVariableIds = new[] { "experiment-variable.prototype.output-quality" }
                }
            });
            SetPrivate(definition, "requiredObservationMethodIds", new[] { "observation-method.close-visual" });
            SetPrivate(definition, "evidencePolicy", new ExperimentPolicyData { minimumTrials = 1, confirmationEvidenceThreshold = 1, minimumEvidenceStrength = 500 });
            SetPrivate(definition, "reproducibilityPolicy", new ExperimentPolicyData { minimumTrials = 2, independentReproductionThreshold = 2 });
            SetPrivate(definition, "confirmationPolicy", new ExperimentPolicyData { minimumTrials = 2, independentReproductionThreshold = 2, confirmationEvidenceThreshold = 2, allowAuthoritativeRegistrationProposal = true });
            return definition;
        }

        private static ExperimentHypothesisData PrototypeHypothesis(TestLabAutomationContext context, string slug)
        {
            return new ExperimentHypothesisData
            {
                hypothesisId = context.ScenarioContext.ScopedId("experiment-hypothesis", slug),
                claim = new HypothesisClaimData
                {
                    claimType = HypothesisClaimType.MaterialSubstitutesForMaterial,
                    subjectId = "recipe.prototype.sword",
                    predicateId = "predicate.prototype.substitution",
                    objectId = "material.prototype.recipe-iron",
                    proposedStableValueId = "material.prototype.recipe-iron",
                    displayText = "Prototype iron can satisfy the tested sword material slot."
                },
                targetRecipeId = "recipe.prototype.sword",
                targetMaterialId = "material.prototype.recipe-iron",
                authorPersonId = context.ScenarioContext.Runtimes.PersonId,
                creationWorldTime = "0",
                testability = HypothesisTestabilityState.Testable,
                status = HypothesisStatus.Proposed,
                confidence = 250
            };
        }

        private static ExperimentPlanData PrototypeExperimentPlan(TestLabAutomationContext context, string experimentId, string hypothesisId, string recipeId = "")
        {
            return new ExperimentPlanData
            {
                planId = context.ScenarioContext.ScopedId("experiment-plan", string.IsNullOrWhiteSpace(recipeId) ? "controlled" : "recipe"),
                experimentDefinitionId = experimentId,
                mode = ExperimentPlanMode.Controlled,
                hypothesisIds = new[] { hypothesisId },
                targetSubjectIds = new[] { string.IsNullOrWhiteSpace(recipeId) ? "recipe.prototype.sword" : recipeId },
                recipeDefinitionId = string.IsNullOrWhiteSpace(recipeId) ? "recipe.prototype.sword" : recipeId,
                recipeVersionId = "recipe-version.prototype.sword.v1",
                trialCount = 2,
                variables =
                {
                    new ExperimentVariableAssignmentData
                    {
                        assignmentId = context.ScenarioContext.ScopedId("experiment-variable-assignment", "material"),
                        variableId = "experiment-variable.prototype.substitute-material",
                        valueType = ExperimentValueType.StableId,
                        stableValueId = "material.prototype.recipe-iron"
                    }
                },
                controls =
                {
                    new ExperimentControlAssignmentData
                    {
                        assignmentId = context.ScenarioContext.ScopedId("experiment-control-assignment", "known-iron"),
                        controlId = "experiment-control.prototype.known-iron",
                        baselineReferenceId = "recipe.prototype.sword",
                        heldVariableIds = new[] { "experiment-variable.prototype.output-quality" }
                    }
                },
                deterministicSeed = $"seed.{context.RunId}.{context.CurrentScenarioId}"
            };
        }

        private static KnowledgeObservationRequest ExperimentObservation(TestLabAutomationContext context, string slug, string recipeId)
        {
            return new KnowledgeObservationRequest
            {
                PersonId = context.ScenarioContext.Runtimes.PersonId,
                TransactionId = context.ScenarioContext.ScopedId("knowledge-observation", slug),
                EvidenceId = context.ScenarioContext.ScopedId("knowledge-evidence", slug),
                Proposition = new KnowledgePropositionData
                {
                    factDefinitionId = "fact.prototype.experiment.recipe-fragment",
                    subjectType = KnowledgeSubjectType.Item,
                    subjectId = recipeId,
                    objectType = KnowledgeSubjectType.Item,
                    objectId = "recipe-input.prototype.iron",
                    valueType = KnowledgeValueType.StableId,
                    stableValueId = "recipe-input.prototype.iron"
                },
                AcquisitionSource = KnowledgeAcquisitionSource.PersonalExperience,
                Provenance = KnowledgeProvenance.Inference,
                Strength = 760,
                Credibility = 760,
                Visibility = KnowledgeVisibility.Public,
                Tags = new[] { "experiment", "recipe-fragment" }
            };
        }

        private static KnowledgeFactDefinition ExperimentKnowledgeFact()
        {
            KnowledgeFactDefinition fact = UnityEngine.ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            SetPrivate(fact, "factId", "fact.prototype.experiment.recipe-fragment");
            SetPrivate(fact, "displayName", "Prototype Experiment Recipe Fragment");
            SetPrivate(fact, "domain", KnowledgeDomain.Crafting);
            SetPrivate(fact, "propositionType", KnowledgePropositionType.Capability);
            SetPrivate(fact, "subjectType", KnowledgeSubjectType.Item);
            SetPrivate(fact, "objectType", KnowledgeSubjectType.Item);
            SetPrivate(fact, "valueType", KnowledgeValueType.StableId);
            SetPrivate(fact, "certaintyThreshold", 700);
            SetPrivate(fact, "requiredEvidenceCount", 1);
            return fact;
        }

        private static TestLabAutomationStepResult DistinctInstances(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-distinct", failure);
            }

            ItemInstanceOperationResult first = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "a"), ownerPersonId: "person.prototype.owner-a");
            ItemInstanceOperationResult second = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "b"), ownerPersonId: "person.prototype.owner-b");
            if (!first.Succeeded || !second.Succeeded)
            {
                return Fail(context, "step9-items-distinct", $"{first.Status}/{second.Status} {first.Message} {second.Message}");
            }

            bool valid = first.Snapshot.ItemInstanceId != second.Snapshot.ItemInstanceId
                && first.Snapshot.ItemDefinitionId == SwordId
                && runtime.QueryByDefinition(SwordId).Count == 2;
            return valid
                ? Pass(context, "step9-items-distinct", $"First={first.Snapshot.ItemInstanceId} Second={second.Snapshot.ItemInstanceId}")
                : Fail(context, "step9-items-distinct", "Distinct item identity failed.");
        }

        private static TestLabAutomationStepResult OwnershipCustody(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-ownership", failure);
            }

            string id = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "owned"), ownerPersonId: "person.owner", custodianPersonId: "person.borrower").Snapshot.ItemInstanceId;
            ItemInstanceOperationResult custody = runtime.TransferCustody(id, custodianPersonId: "person.carrier");
            ItemInstanceOperationResult ownership = runtime.TransferOwnership(id, ItemOwnershipKind.PersonOwned, ownerPersonId: "person.new-owner");
            bool valid = custody.Succeeded
                && ownership.Succeeded
                && ownership.Snapshot.OwnerPersonId == "person.new-owner"
                && ownership.Snapshot.CustodianPersonId == "person.carrier";
            return valid
                ? Pass(context, "step9-items-ownership", $"Owner={ownership.Snapshot.OwnerPersonId} Custodian={ownership.Snapshot.CustodianPersonId}")
                : Fail(context, "step9-items-ownership", $"{custody.Status}/{ownership.Status} {custody.Message} {ownership.Message}");
        }

        private static TestLabAutomationStepResult LocationValidation(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-location", failure);
            }

            string first = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "placed-a")).Snapshot.ItemInstanceId;
            string second = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "placed-b")).Snapshot.ItemInstanceId;
            ItemInstanceOperationResult placed = runtime.SetWorldPlacement(first, context.ScenarioContext.ScopedId("placement", "sword"), "world-entity.prototype.sword-a", "scene.prototype");
            ItemInstanceOperationResult duplicate = runtime.SetWorldPlacement(second, context.ScenarioContext.ScopedId("placement", "sword"), "world-entity.prototype.sword-b", "scene.prototype");
            bool valid = placed.Succeeded && !duplicate.Succeeded && duplicate.Status == ItemInstanceOperationStatus.InvalidLocation;
            return valid
                ? Pass(context, "step9-items-location", duplicate.Message)
                : Fail(context, "step9-items-location", $"{placed.Status}/{duplicate.Status} {placed.Message} {duplicate.Message}");
        }

        private static TestLabAutomationStepResult PersistenceRoundTrip(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-persistence", failure);
            }

            string id = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "persist"), creatorPersonId: "person.smith", ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            runtime.Rename(id, "Persistent Prototype Sword");
            runtime.AssignMakerMarkAndSerial(id, "smith.mark", "P-0001");
            ItemInstanceRuntimeSaveData saveData = runtime.CreateSaveData();
            ItemInstanceIdentityRuntime restored = new ItemInstanceIdentityRuntime();
            ItemInstanceOperationResult restore = restored.RestoreFromSaveData(saveData, context.ScenarioContext.Runtimes.DefinitionRegistry);
            ItemInstanceSnapshot snapshot = null;
            bool valid = restore.Succeeded
                && restored.TryGetSnapshot(id, out snapshot)
                && snapshot.CustomName == "Persistent Prototype Sword"
                && snapshot.MakerMark == "smith.mark";
            return valid
                ? Pass(context, "step9-items-persistence", $"Restored={id} Revision={snapshot.Revision}")
                : Fail(context, "step9-items-persistence", restore.Message);
        }

        private static TestLabAutomationStepResult AccessSubject(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-access", failure);
            }

            string id = runtime.CreateItem(sword, itemInstanceId: RunGuid(context, "subject"), ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            ItemInstanceProjection projection = runtime.Project(id, ItemProjectionAudience.PublicInspection);
            InformationSubjectReferenceData subject = projection.Snapshot.CreateInformationSubject();
            bool valid = !projection.Denied
                && subject.subjectType == InformationSubjectType.Custom
                && subject.subjectId == id
                && subject.parentSubjectId == SwordId
                && subject.tags.Contains("item.instance")
                && subject.tags.Contains(ItemInformationSubject.ItemInstanceSubjectTag)
                && ItemInformationSubject.ProtectedFields.Contains("serial")
                && ItemInformationSubject.ProtectedFields.Contains("provenance");
            return valid
                ? Pass(context, "step9-items-access", $"Subject={subject.subjectId} Parent={subject.parentSubjectId}")
                : Fail(context, "step9-items-access", "Item access subject projection was invalid.");
        }

        private static TestLabAutomationStepResult InventoryEquipmentMigration(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out _, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-migration", failure);
            }

            DefinitionRegistry registry = context.ScenarioContext.Runtimes.DefinitionRegistry;
            if (!registry.TryGet(PotionId, out ItemDefinition potion))
            {
                return Fail(context, "step9-items-migration", $"Item definition '{PotionId}' is missing.");
            }

            string inventorySword = RunGuid(context, "migration-inventory-sword");
            string equippedSword = RunGuid(context, "migration-equipped-sword");
            PlayerInventoryEquipmentSaveData projection = new PlayerInventoryEquipmentSaveData
            {
                inventory = new InventorySaveData
                {
                    slotCapacity = 3,
                    entries =
                    {
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.DefinitionStack, definitionId = potion.ItemId, quantity = 2 },
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = inventorySword, quantity = 1 }
                    }
                },
                equipment = new EquipmentSaveData
                {
                    slots =
                    {
                        new EquipmentSlotSaveData { slotType = EquipmentSlotType.MainHand, mode = EquipmentEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = equippedSword }
                    }
                }
            };

            ItemIdentityInventoryBridgeResult migration = ItemIdentityInventoryBridge.MigrateInventoryEquipmentSave(projection, registry, "person.prototype.player", context.ScenarioContext.Namespace);
            if (!migration.Succeeded)
            {
                return Fail(context, "step9-items-migration", $"{migration.Status}: {migration.Message}");
            }

            ItemIdentityInventoryBridgeResult audit = ItemIdentityInventoryBridge.ValidateInventoryEquipmentProjection(projection, migration.SaveData, "person.prototype.player");
            bool valid = audit.Succeeded
                && migration.SaveData.records.Count == 3
                && migration.SaveData.records.Any(record => record.itemInstanceId == inventorySword && record.location.kind == ItemLocationKind.Inventory)
                && migration.SaveData.records.Any(record => record.itemInstanceId == equippedSword && record.location.kind == ItemLocationKind.Equipped)
                && migration.SaveData.records.Any(record => record.itemDefinitionId == PotionId && record.classification == ItemInstanceClassification.Fungible && record.stackQuantity == 2);
            return valid
                ? Pass(context, "step9-items-migration", $"Migrated={migration.SaveData.records.Count} Audit={audit.Status}")
                : Fail(context, "step9-items-migration", $"{audit.Status}: {audit.Message}");
        }

        private static TestLabAutomationStepResult InventoryEquipmentSynchronization(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-items-sync", failure);
            }

            string swordId = RunGuid(context, "sync-sword");
            PlayerInventoryEquipmentSaveData projection = new PlayerInventoryEquipmentSaveData
            {
                inventory = new InventorySaveData
                {
                    slotCapacity = 1,
                    entries =
                    {
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = swordId }
                    }
                },
                equipment = new EquipmentSaveData()
            };

            ItemIdentityInventoryBridgeResult first = ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime(
                runtime,
                projection,
                context.ScenarioContext.Runtimes.DefinitionRegistry,
                "person.prototype.player",
                context.ScenarioContext.Namespace);
            projection.inventory.entries.Clear();
            projection.equipment.slots.Add(new EquipmentSlotSaveData { slotType = EquipmentSlotType.MainHand, mode = EquipmentEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = swordId });
            ItemIdentityInventoryBridgeResult second = ItemIdentityInventoryBridge.SynchronizeInventoryEquipmentRuntime(
                runtime,
                projection,
                context.ScenarioContext.Runtimes.DefinitionRegistry,
                "person.prototype.player",
                context.ScenarioContext.Namespace);
            ItemIdentityInventoryBridgeResult audit = ItemIdentityInventoryBridge.ValidateSynchronizedProjection(
                projection,
                runtime.CreateSaveData(),
                context.ScenarioContext.Runtimes.DefinitionRegistry,
                "person.prototype.player",
                context.ScenarioContext.Namespace);

            ItemInstanceSnapshot snapshot = null;
            bool valid = first.Succeeded
                && second.Succeeded
                && audit.Succeeded
                && runtime.TryGetSnapshot(swordId, out snapshot)
                && snapshot.LocationKind == ItemLocationKind.Equipped
                && snapshot.Data.location.equipmentSlotId == EquipmentSlotType.MainHand.ToString();
            return valid
                ? Pass(context, "step9-items-sync", $"Sync={first.Status}/{second.Status} Location={snapshot.LocationKind}/{snapshot.Data.location.equipmentSlotId}")
                : Fail(context, "step9-items-sync", $"First={first.Status}:{first.Message} Second={second.Status}:{second.Message} Audit={audit.Status}:{audit.Message}");
        }

        private static TestLabAutomationStepResult MaterialDefinitionsResolve(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out string failure);
            if (registry == null)
            {
                return Fail(context, "step9-materials-resolve", failure);
            }

            ItemCompositionRuntime runtime = context.ScenarioContext.Runtimes.ItemCompositions;
            IReadOnlyList<string> expanded = runtime.ExpandCompositeMaterial("material.prototype.steel", registry);
            bool valid = registry.TryGet("material.prototype.iron", out MaterialDefinition _)
                && registry.TryGet("material.prototype.steel", out MaterialDefinition steel)
                && steel.IsComposite
                && expanded.Contains("material.prototype.iron");
            return valid
                ? Pass(context, "step9-materials-resolve", $"Materials={registry.DefinitionsById.Values.OfType<MaterialDefinition>().Count()} Steel=[{string.Join(",", expanded)}]")
                : Fail(context, "step9-materials-resolve", "Material definitions did not resolve.");
        }

        private static TestLabAutomationStepResult CompositionGraphValidation(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-graph", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            string itemId = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "composition"), ownerPersonId: "person.prototype.player").Snapshot.ItemInstanceId;
            ItemCompositionOperationResult valid = compositions.SetComposition(itemRuntime, registry, Composition(itemId, "material.prototype.iron"));
            ItemCompositionRecordData invalid = Composition(itemId, "material.prototype.missing");
            invalid.compositionId = $"item-composition.{itemId}.invalid";
            invalid.itemInstanceId = RunGuid(context, "missing-item");
            ItemCompositionOperationResult rejected = compositions.SetComposition(itemRuntime, registry, invalid);
            bool ok = valid.Succeeded && !rejected.Succeeded && compositions.TryGetSnapshotForItem(itemId, out _);
            return ok
                ? Pass(context, "step9-composition-graph", $"Valid={valid.Status} Rejected={rejected.Status}")
                : Fail(context, "step9-composition-graph", $"Valid={valid.Status}:{valid.Message} Rejected={rejected.Status}:{rejected.Message}");
        }

        private static TestLabAutomationStepResult DefaultComposition(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-default", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            string itemId = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "default-composition")).Snapshot.ItemInstanceId;
            ItemCompositionOperationResult ensured = compositions.EnsureCompositionForItem(itemRuntime, registry, itemId);
            bool valid = ensured.Succeeded
                && ensured.Snapshot.ItemInstanceId == itemId
                && ensured.Snapshot.Completeness == ItemCompositionCompleteness.Unknown;
            return valid
                ? Pass(context, "step9-composition-default", $"Composition={ensured.Snapshot.CompositionId} Completeness={ensured.Snapshot.Completeness}")
                : Fail(context, "step9-composition-default", $"Ensure={ensured.Status}:{ensured.Message}");
        }

        private static TestLabAutomationStepResult AtomicCompositionCreation(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-atomic", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            ItemCompositionCreationResult rejected = ItemCompositionCoordinator.CreateItem(itemRuntime, compositions, registry, new ItemCompositionCreationRequest
            {
                Definition = sword,
                ItemInstanceId = RunGuid(context, "atomic-rejected"),
                RequireComposition = true,
                ExplicitComposition = Composition("placeholder", "material.prototype.missing"),
                Purpose = ItemCompositionMutationPurpose.AuthoredSetup
            });
            ItemCompositionCreationResult committed = ItemCompositionCoordinator.CreateItem(itemRuntime, compositions, registry, new ItemCompositionCreationRequest
            {
                Definition = sword,
                ItemInstanceId = RunGuid(context, "atomic-committed"),
                RequireComposition = true,
                ExplicitComposition = Composition("placeholder", "material.prototype.iron"),
                Purpose = ItemCompositionMutationPurpose.AuthoredSetup
            });

            bool valid = !rejected.Succeeded
                && itemRuntime.QueryByDefinition(SwordId).Count == 1
                && committed.Succeeded
                && compositions.TryGetSnapshotForItem(committed.Item.ItemInstanceId, out _);
            return valid
                ? Pass(context, "step9-composition-atomic", $"Rejected={rejected.Status} Committed={committed.Item.ItemInstanceId}")
                : Fail(context, "step9-composition-atomic", $"Rejected={rejected.Status}:{rejected.Message} Committed={committed.Status}:{committed.Message} Items={itemRuntime.Count} Compositions={compositions.Count}");
        }

        private static TestLabAutomationStepResult DerivedProperties(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-properties", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: true, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            string itemId = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "properties")).Snapshot.ItemInstanceId;
            ItemCompositionRecordData record = Composition(itemId, "material.prototype.iron");
            record.materials.Add(MaterialEntry("entry.oil", "material.prototype.oil", MaterialEntryRole.Coating, 100f, MaterialQuantityUnit.Milliliter));
            ItemCompositionOperationResult set = compositions.SetComposition(itemRuntime, registry, record);
            compositions.TryGetSnapshotForItem(itemId, out ItemCompositionSnapshot snapshot);
            DerivedItemMaterialProperties properties = compositions.ComputeDerivedProperties(snapshot, registry);
            MaterialCompatibilityEvaluation compatibility = compositions.EvaluateCompatibility(snapshot.Materials[0], snapshot.Materials[1], registry);
            bool valid = set.Succeeded && properties.KnownMassKg > 0f && compatibility.Outcome == MaterialCompatibilityOutcome.Degrades;
            return valid
                ? Pass(context, "step9-composition-properties", $"Mass={properties.KnownMassKg:0.###} Compatibility={compatibility.RuleId}:{compatibility.Outcome}")
                : Fail(context, "step9-composition-properties", $"Set={set.Status}:{set.Message} Mass={properties.KnownMassKg} Compatibility={compatibility.Outcome}");
        }

        private static TestLabAutomationStepResult TrackedComponents(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-tracked", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            ItemDefinition potion = registry.TryGet(PotionId, out ItemDefinition foundPotion) ? foundPotion : sword;
            string parent = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "tracked-parent")).Snapshot.ItemInstanceId;
            string child = itemRuntime.CreateItem(potion, itemInstanceId: RunGuid(context, "tracked-child"), ownerPersonId: "person.prototype.player", custodianPersonId: "person.prototype.player").Snapshot.ItemInstanceId;
            ItemCompositionRecordData invalid = Composition(parent, "material.prototype.iron");
            invalid.components.Add(new ItemComponentEntryData { componentEntryId = "component.socketed-gem", kind = ItemComponentKind.TrackedItemInstance, componentItemInstanceId = child });
            ItemCompositionOperationResult rejected = compositions.SetComposition(itemRuntime, registry, invalid);
            ItemCompositionOperationResult attached = ItemCompositionCoordinator.AttachTrackedComponent(itemRuntime, compositions, registry, parent, child, new ItemComponentEntryData { componentEntryId = "component.socketed-gem" }, ItemCompositionMutationPurpose.DebugTestLab);
            ItemCompositionOperationResult duplicate = ItemCompositionCoordinator.AttachTrackedComponent(itemRuntime, compositions, registry, RunGuid(context, "missing-parent"), child, new ItemComponentEntryData { componentEntryId = "component.other" }, ItemCompositionMutationPurpose.DebugTestLab);
            itemRuntime.TryGetSnapshot(child, out ItemInstanceSnapshot childSnapshot);
            bool valid = rejected.Status == ItemCompositionOperationStatus.InvalidComponentLocation
                && attached.Succeeded
                && !duplicate.Succeeded
                && childSnapshot != null
                && childSnapshot.LocationKind == ItemLocationKind.ProductionReserved;
            return valid
                ? Pass(context, "step9-composition-tracked", $"Rejected={rejected.Status} Attached={attached.Status} Child={childSnapshot.LocationKind}")
                : Fail(context, "step9-composition-tracked", $"Rejected={rejected.Status}:{rejected.Message} Attached={attached.Status}:{attached.Message} Duplicate={duplicate.Status}:{duplicate.Message}");
        }

        private static TestLabAutomationStepResult CompositeExpansion(TestLabAutomationContext context)
        {
            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out string failure, includeComposite: true);
            if (registry == null)
            {
                return Fail(context, "step9-composition-composite", failure);
            }

            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            CompositeMaterialExpansionResult expanded = compositions.ExpandCompositeMaterialConstituents("material.prototype.pattern-weld", registry);
            CompositeMaterialExpansionResult shallow = compositions.ExpandCompositeMaterialConstituents("material.prototype.pattern-weld", registry, expandNested: false);
            bool valid = expanded.Succeeded
                && expanded.Entries.Any(entry => entry.MaterialDefinitionId == "material.prototype.iron" && Math.Abs(entry.Ratio - 1f) < 0.0001f)
                && shallow.Succeeded
                && shallow.Entries.Any(entry => entry.MaterialDefinitionId == "material.prototype.pattern-weld");
            return valid
                ? Pass(context, "step9-composition-composite", $"Expanded=[{string.Join(",", expanded.Entries.Select(entry => $"{entry.MaterialDefinitionId}:{entry.Ratio:0.###}"))}]")
                : Fail(context, "step9-composition-composite", $"Expanded={expanded.Succeeded}:{expanded.Message} Shallow={shallow.Succeeded}:{shallow.Message}");
        }

        private static TestLabAutomationStepResult StackEquivalence(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-stack", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            string first = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "stack-a")).Snapshot.ItemInstanceId;
            string second = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "stack-b")).Snapshot.ItemInstanceId;
            compositions.SetComposition(itemRuntime, registry, Composition(first, "material.prototype.iron"));
            compositions.SetComposition(itemRuntime, registry, Composition(second, "material.prototype.wood"));
            bool differentRejected = !compositions.CanShareStack(first, second);
            ItemCompositionRecordData equivalent = Composition(second, "material.prototype.iron");
            equivalent.materials[0].quantity = new MaterialQuantityData { value = 1200f, unit = MaterialQuantityUnit.Gram };
            compositions.SetComposition(itemRuntime, registry, equivalent);
            bool sameAccepted = compositions.CanShareStack(first, second);
            return differentRejected && sameAccepted
                ? Pass(context, "step9-composition-stack", $"DifferentRejected={differentRejected} SameAccepted={sameAccepted}")
                : Fail(context, "step9-composition-stack", $"DifferentRejected={differentRejected} SameAccepted={sameAccepted}");
        }

        private static TestLabAutomationStepResult ProjectionAndPersistence(TestLabAutomationContext context)
        {
            if (!TryCreateRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-composition-project-save", failure);
            }

            DefinitionRegistry registry = CreateCompositionRegistry(context, includeRule: false, out failure);
            ItemCompositionRuntime compositions = context.ScenarioContext.Runtimes.ItemCompositions;
            string itemId = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "persist-composition")).Snapshot.ItemInstanceId;
            ItemCompositionRecordData record = Composition(itemId, "material.prototype.iron");
            record.materials[0].purity = 0.5f;
            ItemCompositionOperationResult set = compositions.SetComposition(itemRuntime, registry, record);
            InformationAccessDecision decision = new InformationAccessDecision("person.viewer", ItemCompositionInformationSubject.Create(itemId, record.compositionId, SwordId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.None, true, InformationResharingPolicy.NoResharing, Array.Empty<string>(), ItemCompositionInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { "policy.prototype.materials" }, 0d, "Redacted", "Test Lab redaction", false);
            ItemCompositionProjection projection = compositions.Project(itemId, decision);
            ItemCompositionRuntimeSaveData saveData = compositions.CreateSaveData();
            ItemCompositionRuntime restored = new ItemCompositionRuntime();
            ItemCompositionOperationResult restore = restored.RestoreFromSaveData(saveData, registry, itemRuntime);
            ItemCompositionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.records[0].materials[0].materialDefinitionId = "material.prototype.missing";
            bool corruptRejected = !ItemCompositionRuntime.ValidateSaveData(corrupt, registry, itemRuntime, out _);
            bool noLeak = projection.Snapshot != null
                && projection.Snapshot.Data.revisionHistory.Count == 0
                && projection.Snapshot.Data.provenanceIds.Length == 0
                && projection.VisibleMaterials.All(material => !material.hidden && material.purity <= 0f);
            bool valid = set.Succeeded && projection.Redacted && noLeak && restore.Succeeded && restored.TryGetSnapshotForItem(itemId, out _) && corruptRejected;
            return valid
                ? Pass(context, "step9-composition-project-save", $"ProjectionRedacted={projection.Redacted} NoLeak={noLeak} Restore={restore.Status} CorruptRejected={corruptRejected}")
                : Fail(context, "step9-composition-project-save", $"Set={set.Status}:{set.Message} Projection={projection.Redacted} NoLeak={noLeak} Restore={restore.Status}:{restore.Message} CorruptRejected={corruptRejected}");
        }

        private static TestLabAutomationStepResult QualityWorkmanship(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition _, out string failure))
            {
                return Fail(context, "step9-quality-workmanship", failure);
            }

            string ordinary = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "ordinary");
            string masterwork = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "masterwork");
            ItemQualityAffixOperationResult defaultQuality = quality.EnsureDefaultQuality(itemRuntime, compositions, registry, ordinary);
            ItemQualityAffixOperationResult masterworkQuality = quality.SetQualityRecord(itemRuntime, compositions, registry, new ItemQualityRecordData
            {
                itemInstanceId = masterwork,
                itemDefinitionId = SwordId,
                overallQuality = 0.95f,
                source = ItemQualityRecordSource.Authored,
                workmanship =
                {
                    Workmanship("workmanship.overall", WorkmanshipDimension.Overall, 0.95f),
                    Workmanship("workmanship.balance", WorkmanshipDimension.Balance, 0.9f),
                    new ItemWorkmanshipEntryData { entryId = "workmanship.decoration.na", dimension = WorkmanshipDimension.Decoration, value = new ItemQualityValueData { state = QualityValueState.NotApplicable, value = -1f } }
                },
                dimensions =
                {
                    Dimension("quality.structural", ItemQualityDimension.Structural, 0.92f, 1f),
                    Dimension("quality.functional", ItemQualityDimension.Functional, 0.96f, 1f)
                }
            });

            bool valid = defaultQuality.Succeeded
                && masterworkQuality.Succeeded
                && masterworkQuality.Quality.QualityTierId == "quality.masterwork"
                && masterworkQuality.Quality.Data.workmanship.Any(entry => entry.value.state == QualityValueState.NotApplicable);
            return valid
                ? Pass(context, "step9-quality-workmanship", $"Default={defaultQuality.Quality.QualityTierId} Masterwork={masterworkQuality.Quality.QualityTierId}")
                : Fail(context, "step9-quality-workmanship", $"Default={defaultQuality.Status} Masterwork={masterworkQuality.Status}/{masterworkQuality.Quality?.QualityTierId}");
        }

        private static TestLabAutomationStepResult DefectsAndRedaction(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition _, out string failure))
            {
                return Fail(context, "step9-quality-defects", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "defects");
            quality.EnsureDefaultQuality(itemRuntime, compositions, registry, item);
            ItemQualityAffixOperationResult visible = quality.AddDefect(itemRuntime, compositions, registry, item, new ItemDefectEntryData { defectId = RunGuid(context, "defect.visible"), category = ItemDefectCategory.Dull, severity = 0.2f, hidden = false });
            ItemQualityAffixOperationResult hidden = quality.AddDefect(itemRuntime, compositions, registry, item, new ItemDefectEntryData { defectId = RunGuid(context, "defect.hidden"), category = ItemDefectCategory.HiddenDefect, severity = 0.4f, hidden = true });
            InformationAccessDecision decision = new InformationAccessDecision("person.viewer", ItemQualityAffixInformationSubject.Quality(item, $"item-quality.{item}", SwordId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.None, true, InformationResharingPolicy.NoResharing, Array.Empty<string>(), ItemQualityAffixInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { "policy.prototype.quality" }, 0d, "Redacted", "Test Lab redaction", false);
            ItemQualityProjection projection = quality.Project(item, decision);
            bool hasAuthoritative = quality.TryGetQualityForItem(item, out ItemQualitySnapshot reread);

            bool valid = visible.Succeeded
                && hidden.Succeeded
                && projection.Redacted
                && projection.Snapshot.Data.defects.Count == 1
                && !projection.Snapshot.Data.defects[0].hidden
                && hasAuthoritative
                && reread.Data.defects.Count == 2;
            return valid
                ? Pass(context, "step9-quality-defects", $"ProjectionDefects={projection.Snapshot.Data.defects.Count} Authoritative={reread.Data.defects.Count}")
                : Fail(context, "step9-quality-defects", $"Visible={visible.Status} Hidden={hidden.Status} Projection={projection.Redacted}/{projection.Snapshot?.Data.defects.Count}");
        }

        private static TestLabAutomationStepResult AffixGenerationDeterministic(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition _, out string failure))
            {
                return Fail(context, "step9-quality-generation", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "generated");
            quality.SetQualityRecord(itemRuntime, compositions, registry, QualityRecord(item, 0.8f));
            ItemAffixGenerationRequest request = new ItemAffixGenerationRequest { ItemInstanceId = item, Seed = "seed.prototype.fixed", RequestedAffixCount = 1, Preview = true };
            ItemQualityAffixOperationResult preview = quality.GenerateAffixes(itemRuntime, compositions, registry, request);
            request.Preview = false;
            ItemQualityAffixOperationResult execute = quality.GenerateAffixes(itemRuntime, compositions, registry, request);
            ItemQualityAffixRuntimeSaveData save = quality.CreateSaveData();
            ItemQualityAffixOperationResult duplicate = quality.GenerateAffixes(itemRuntime, compositions, registry, request);

            bool same = preview.Succeeded
                && execute.Succeeded
                && preview.Affixes[0].AffixDefinitionId == execute.Affixes[0].AffixDefinitionId
                && Math.Abs(preview.Affixes[0].Data.rolledValues[0].value - execute.Affixes[0].Data.rolledValues[0].value) < 0.0001f
                && !duplicate.Succeeded
                && save.affixInstances.Count == 1;
            return same
                ? Pass(context, "step9-quality-generation", $"Affix={execute.Affixes[0].AffixDefinitionId} Value={execute.Affixes[0].Data.rolledValues[0].value:0.###}")
                : Fail(context, "step9-quality-generation", $"Preview={preview.Status} Execute={execute.Status} Duplicate={duplicate.Status} Count={save.affixInstances.Count}");
        }

        private static TestLabAutomationStepResult AffixConflictAndStack(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition keen, out string failure))
            {
                return Fail(context, "step9-quality-conflict-stack", failure);
            }

            string first = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "stack-a");
            string second = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "stack-b");
            quality.SetQualityRecord(itemRuntime, compositions, registry, QualityRecord(first, 0.8f));
            quality.SetQualityRecord(itemRuntime, compositions, registry, QualityRecord(second, 0.4f));
            ItemQualityAffixOperationResult applied = quality.ApplyAffix(itemRuntime, compositions, registry, first, keen, seed: "same");
            ItemQualityAffixOperationResult conflict = quality.ApplyAffix(itemRuntime, compositions, registry, first, keen, seed: "same");
            bool stackDifferent = !quality.CanShareQualityAffixStack(first, second);

            bool valid = applied.Succeeded && !conflict.Succeeded && stackDifferent;
            return valid
                ? Pass(context, "step9-quality-conflict-stack", $"Applied={applied.Status} Conflict={conflict.Status} StackDifferent={stackDifferent}")
                : Fail(context, "step9-quality-conflict-stack", $"Applied={applied.Status} Conflict={conflict.Status} StackDifferent={stackDifferent}");
        }

        private static TestLabAutomationStepResult AffixModifierContribution(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition keen, out string failure))
            {
                return Fail(context, "step9-quality-modifiers", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "modifier");
            quality.SetQualityRecord(itemRuntime, compositions, registry, QualityRecord(item, 0.8f));
            ItemQualityAffixOperationResult applied = quality.ApplyAffix(itemRuntime, compositions, registry, item, keen, seed: "modifier");
            RuntimeStatCollection stats = new RuntimeStatCollection();
            stats.SetBaseValue(StatType.AttackPower, 10f);
            ItemQualityAffixOperationResult first = quality.ApplyActiveAffixModifiers(item, registry, stats);
            ItemQualityAffixOperationResult second = quality.ApplyActiveAffixModifiers(item, registry, stats);
            float afterApply = stats.GetValue(StatType.AttackPower);
            quality.RemoveActiveAffixModifiers(item, stats);
            float afterRemove = stats.GetValue(StatType.AttackPower);

            bool valid = applied.Succeeded && first.Succeeded && second.Succeeded && Math.Abs(afterApply - 12f) < 0.001f && Math.Abs(afterRemove - 10f) < 0.001f;
            return valid
                ? Pass(context, "step9-quality-modifiers", $"Apply={afterApply} Remove={afterRemove}")
                : Fail(context, "step9-quality-modifiers", $"Applied={applied.Status} First={first.Status} Second={second.Status} Apply={afterApply} Remove={afterRemove}");
        }

        private static TestLabAutomationStepResult QualityPersistenceAndMigration(TestLabAutomationContext context)
        {
            if (!TryCreateQualityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out DefinitionRegistry registry, out ItemDefinition sword, out _, out ItemAffixDefinition keen, out string failure))
            {
                return Fail(context, "step9-quality-persistence", failure);
            }

            string item = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "quality-persistence"), creationSourceId: "feature.9.5").Snapshot.ItemInstanceId;
            ItemQualityAffixOperationResult authored = quality.SetQualityRecord(itemRuntime, compositions, registry, QualityRecord(item, 0.82f));
            ItemQualityAffixOperationResult affix = quality.ApplyAffix(itemRuntime, compositions, registry, item, keen, seed: "restore");
            ItemQualityAffixRuntimeSaveData save = quality.CreateSaveData();
            ItemQualityAffixRuntime restored = new ItemQualityAffixRuntime();
            ItemQualityAffixOperationResult restore = restored.RestoreFromSaveData(save, registry, itemRuntime);
            ItemQualityAffixRuntimeSaveData corrupt = save.Clone();
            corrupt.affixInstances[0].affixDefinitionId = "affix.prototype.missing";
            bool corruptRejected = !ItemQualityAffixRuntime.ValidateSaveData(corrupt, registry, itemRuntime, out _);

            bool valid = authored.Succeeded
                && affix.Succeeded
                && restore.Succeeded
                && restored.GetAffixesForItem(item).Count == 1
                && Math.Abs(restored.GetAffixesForItem(item)[0].Data.rolledValues[0].value - affix.Affixes[0].Data.rolledValues[0].value) < 0.0001f
                && corruptRejected;
            return valid
                ? Pass(context, "step9-quality-persistence", $"Authored={authored.Quality.QualityTierId} Restore={restore.Status} CorruptRejected={corruptRejected}")
                : Fail(context, "step9-quality-persistence", $"Authored={authored.Status} Affix={affix.Status} Restore={restore.Status} Count={restored.GetAffixesForItem(item).Count} Corrupt={corruptRejected}");
        }

        private static TestLabAutomationStepResult DurabilityMigration(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-migration", failure);
            }

            string item = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "durability-migration"), creationSourceId: "feature.9.1").Snapshot.ItemInstanceId;
            compositions.SetComposition(itemRuntime, registry, Composition(item, "material.prototype.iron"));
            ItemDurabilityOperationResult ensured = durability.EnsureDefaultDurability(itemRuntime, compositions, quality, registry, item);
            bool valid = ensured.Succeeded
                && ensured.Snapshot.NormalizedDurability >= 0.999f
                && ensured.Snapshot.Data.source == ItemDurabilityRecordSource.DefinitionDefault
                && ensured.Snapshot.Data.relatedItemRevision > 0L;
            return valid
                ? Pass(context, "step9-durability-migration", $"Condition={ensured.Snapshot.ConditionBandId} Current={ensured.Snapshot.CurrentDurability:0.###}/{ensured.Snapshot.MaximumDurability:0.###}")
                : Fail(context, "step9-durability-migration", $"Ensure={ensured.Status}:{ensured.Message} Condition={ensured.Snapshot?.ConditionBandId}");
        }

        private static TestLabAutomationStepResult DurabilityDamage(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-damage", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-damage");
            durability.EnsureDefaultDurability(itemRuntime, compositions, quality, registry, item);
            ItemDurabilityOperationResult preview = durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 30f, ItemDamageChannel.Impact, "component.blade", "preview", preview: true);
            ItemDurabilityOperationResult apply = durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 30f, ItemDamageChannel.Impact, "component.blade", "impact");
            ItemDurabilityOperationResult componentMissing = durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 1f, ItemDamageChannel.Impact, "component.missing", "bad");
            bool valid = preview.Preview
                && apply.Succeeded
                && !componentMissing.Succeeded
                && apply.Snapshot.Data.damageChannels.Any(channel => channel.channel == ItemDamageChannel.Impact && channel.accumulatedDamage >= 30f)
                && apply.Snapshot.Components.Any(component => component.componentEntryId == "component.blade" && component.currentDurability < component.maximumDurability);
            return valid
                ? Pass(context, "step9-durability-damage", $"Preview={preview.Status} Apply={apply.Status} Component={apply.Snapshot.Components[0].functionalState}")
                : Fail(context, "step9-durability-damage", $"Preview={preview.Status}:{preview.Message} Apply={apply.Status}:{apply.Message} Missing={componentMissing.Status}:{componentMissing.Message}");
        }

        private static TestLabAutomationStepResult DurabilityRepair(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-repair", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-repair");
            ItemDurabilityOperationResult damaged = durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 70f, ItemDamageChannel.Cutting, "component.blade", "damage", permanent: true);
            float before = damaged.Snapshot.CurrentDurability;
            ItemDurabilityOperationResult repaired = durability.Repair(itemRuntime, compositions, quality, registry, item, 35f, ItemRepairQuality.Adequate, "component.blade", "repair.test", "person.smith", "repair");
            bool valid = repaired.Succeeded
                && repaired.Snapshot.CurrentDurability > before
                && repaired.Snapshot.Data.maximumDurability < repaired.Snapshot.Data.originalMaximumDurability
                && repaired.Snapshot.Data.repairHistory.Any(record => record.repairId == "repair.test");
            return valid
                ? Pass(context, "step9-durability-repair", $"Before={before:0.###} After={repaired.Snapshot.CurrentDurability:0.###} Max={repaired.Snapshot.MaximumDurability:0.###}/{repaired.Snapshot.Data.originalMaximumDurability:0.###}")
                : Fail(context, "step9-durability-repair", $"Damaged={damaged.Status}:{damaged.Message} Repaired={repaired.Status}:{repaired.Message}");
        }

        private static TestLabAutomationStepResult DurabilitySalvagePersistence(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-salvage-save", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-salvage");
            durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 999f, ItemDamageChannel.Crushing, "component.blade", "break");
            ItemDurabilityOperationResult preview = durability.PreviewSalvage(item);
            ItemDurabilityOperationResult salvage = durability.ExecuteSalvage(itemRuntime, compositions, quality, registry, item, "salvage");
            ItemDurabilityRuntimeSaveData save = durability.CreateSaveData();
            ItemDurabilityRuntime restored = new ItemDurabilityRuntime();
            ItemDurabilityOperationResult restore = restored.RestoreFromSaveData(save, registry, itemRuntime, compositions);
            ItemDurabilityRuntimeSaveData corrupt = save.Clone();
            corrupt.records[0].itemInstanceId = RunGuid(context, "missing-durability-item");
            bool corruptRejected = !ItemDurabilityRuntime.ValidateSaveData(corrupt, registry, itemRuntime, compositions, out _);
            bool valid = preview.Preview
                && preview.SalvageOutputs.Count > 0
                && salvage.Succeeded
                && salvage.Snapshot.Data.salvageState == ItemSalvageState.Salvaged
                && restore.Succeeded
                && restored.TryGetDurabilityForItem(item, out ItemDurabilitySnapshot restoredSnapshot)
                && restoredSnapshot.Data.salvageOutputs.Count == salvage.SalvageOutputs.Count
                && corruptRejected;
            return valid
                ? Pass(context, "step9-durability-salvage-save", $"Outputs={salvage.SalvageOutputs.Count} Restore={restore.Status} CorruptRejected={corruptRejected}")
                : Fail(context, "step9-durability-salvage-save", $"Preview={preview.Status} Salvage={salvage.Status}:{salvage.Message} Restore={restore.Status}:{restore.Message} Corrupt={corruptRejected}");
        }

        private static TestLabAutomationStepResult DurabilityProjectionStack(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-project-stack", failure);
            }

            string first = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-stack-a");
            string second = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-stack-b");
            durability.EnsureDefaultDurability(itemRuntime, compositions, quality, registry, first);
            durability.EnsureDefaultDurability(itemRuntime, compositions, quality, registry, second);
            bool stackSame = durability.CanShareDurabilityStack(first, second);
            durability.ApplyDamage(itemRuntime, compositions, quality, registry, second, 50f, ItemDamageChannel.Cutting, "component.blade", "stack-different");
            bool stackDifferent = !durability.CanShareDurabilityStack(first, second);
            InformationAccessDecision decision = new InformationAccessDecision("person.viewer", ItemDurabilityInformationSubject.Create(first, $"item-durability.{first}", SwordId), InformationAccessMode.Inspect, InformationAccessDecisionKind.RedactedAccess, InformationAccessDenialCode.None, true, InformationResharingPolicy.NoResharing, Array.Empty<string>(), ItemDurabilityInformationSubject.ProtectedFields, Array.Empty<string>(), new[] { "policy.prototype.durability" }, 0d, "Redacted", "Test Lab redaction", false);
            ItemDurabilityProjection projection = durability.Project(first, decision);
            bool valid = stackSame && stackDifferent && projection.Redacted && projection.Snapshot.CreateInformationSubject().tags.Contains(ItemDurabilityInformationSubject.DurabilitySubjectTag);
            return valid
                ? Pass(context, "step9-durability-project-stack", $"StackSame={stackSame} StackDifferent={stackDifferent} Redacted={projection.Redacted}")
                : Fail(context, "step9-durability-project-stack", $"StackSame={stackSame} StackDifferent={stackDifferent} Redacted={projection.Redacted}");
        }

        private static TestLabAutomationStepResult DurabilityEquipmentContribution(TestLabAutomationContext context)
        {
            if (!TryCreateDurabilityRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ItemCompositionRuntime compositions, out ItemQualityAffixRuntime quality, out ItemDurabilityRuntime durability, out DefinitionRegistry registry, out ItemDefinition sword, out string failure))
            {
                return Fail(context, "step9-durability-equipment", failure);
            }

            string item = CreateComposedItem(context, itemRuntime, compositions, registry, sword, "durability-equipped");
            ItemDurabilityOperationResult healthy = durability.EnsureDefaultDurability(itemRuntime, compositions, quality, registry, item);
            float healthyFactor = durability.GetEquipmentContributionFactor(item);
            durability.ApplyDamage(itemRuntime, compositions, quality, registry, item, 999f, ItemDamageChannel.Impact, "component.blade", "break");
            float brokenFactor = durability.GetEquipmentContributionFactor(item);
            bool valid = healthy.Succeeded && Math.Abs(healthyFactor - 1f) < 0.001f && Math.Abs(brokenFactor) < 0.001f;
            return valid
                ? Pass(context, "step9-durability-equipment", $"Healthy={healthyFactor:0.###} Broken={brokenFactor:0.###}")
                : Fail(context, "step9-durability-equipment", $"Healthy={healthy.Status}:{healthyFactor} Broken={brokenFactor}");
        }

        private static TestLabAutomationStepResult ProductionToolStationSelection(TestLabAutomationContext context)
        {
            if (!TryCreateProductionRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out ItemDefinition sword, out ProductionToolDefinition hammer, out ProductionToolDefinition mallet, out ProductionStationDefinition forge, out string failure))
            {
                return Fail(context, "step9-production-selection", failure);
            }

            string malletItem = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "production-mallet"), ownerPersonId: context.ScenarioContext.Runtimes.PersonId).Snapshot.ItemInstanceId;
            production.RegisterStation(forge, context.ScenarioContext.ScopedId("station", "forge"), "location.prototype.smithy");
            ProductionRequirementDefinition toolRequirement = ProductionRequirement("production-requirement.prototype.hammer", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary, category: ProductionToolCategory.Hammering, capabilityId: "tool.capability.strike");
            ProductionRequirementDefinition stationRequirement = ProductionRequirement("production-requirement.prototype.forge", ProductionRequirementType.Station, station: forge, stationCategory: ProductionStationCategory.Forge, stationCapabilityId: "station.capability.heat");
            ProductionContextData productionContext = new ProductionContextData
            {
                actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                locationId = "location.prototype.smithy",
                toolCandidates = { ToolCandidate(malletItem, mallet) }
            };

            ProductionRequirementEvaluationResult first = production.EvaluateRequirements(new[] { toolRequirement, stationRequirement }, productionContext, registry, itemRuntime, productionJobId: context.ScenarioContext.ScopedId("production-job", "selection"));
            ProductionRequirementEvaluationResult second = production.EvaluateRequirements(new[] { stationRequirement, toolRequirement }, productionContext, registry, itemRuntime, productionJobId: context.ScenarioContext.ScopedId("production-job", "selection"));
            ProductionRequirementEvaluationResult perceived = production.EvaluateRequirements(
                new[] { toolRequirement },
                new ProductionContextData
                {
                    actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                    perspective = ProductionEvaluationPerspective.Perceived,
                    toolCandidates = { ToolCandidate(malletItem, mallet, perceived: true, authoritative: false, durability: 1f) }
                },
                registry,
                itemRuntime,
                productionJobId: context.ScenarioContext.ScopedId("production-job", "selection-perceived"));
            ProductionRequirementEvaluationResult authoritative = production.EvaluateRequirements(
                new[] { toolRequirement },
                new ProductionContextData
                {
                    actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                    perspective = ProductionEvaluationPerspective.Authoritative,
                    toolCandidates = { ToolCandidate(malletItem, mallet, perceived: true, authoritative: false, durability: 1f) }
                },
                registry,
                itemRuntime,
                productionJobId: context.ScenarioContext.ScopedId("production-job", "selection-authoritative"));
            bool valid = first.Succeeded
                && second.Succeeded
                && first.Plan.signature == second.Plan.signature
                && first.Plan.selections.Any(selection => selection.selectedToolDefinitionId == mallet.Id)
                && first.Plan.selections.Any(selection => !string.IsNullOrWhiteSpace(selection.selectedStationInstanceId))
                && perceived.Succeeded
                && !authoritative.Succeeded;
            return valid
                ? Pass(context, "step9-production-selection", $"Plan={first.Plan.planId} Signature={first.Plan.signature} Perceived={perceived.Status} Authoritative={authoritative.Status}")
                : Fail(context, "step9-production-selection", $"First={first.Status} Second={second.Status} Perceived={perceived.Status} Authoritative={authoritative.Status} {first.Message} {second.Message}");
        }

        private static TestLabAutomationStepResult ProductionResourceSkillKnowledge(TestLabAutomationContext context)
        {
            if (!TryCreateProductionRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out ItemDefinition sword, out _, out _, out _, out string failure))
            {
                return Fail(context, "step9-production-requirements", failure);
            }

            MaterialDefinition iron = Material("material.prototype.production-iron", MaterialCategory.Metal, 7.8f, 0.8f, 0.8f);
            registry = ExtendRegistry(registry, iron);
            ProductionRequirementDefinition skill = ProductionRequirement("production-requirement.prototype.skill", ProductionRequirementType.SkillCapability, capabilityId: "capability.production.blacksmithing");
            ProductionRequirementDefinition knowledge = ProductionRequirement("production-requirement.prototype.knowledge", ProductionRequirementType.Knowledge, knowledgeId: "fact.prototype.blade-pattern");
            ProductionRequirementDefinition heat = ProductionRequirement("production-requirement.prototype.heat", ProductionRequirementType.Resource, resourceId: "resource.production.heat", quantity: 5f);
            ProductionRequirementDefinition item = ProductionRequirement("production-requirement.prototype.blank", ProductionRequirementType.Item, item: sword, quantity: 1f);
            ProductionRequirementDefinition material = ProductionRequirement("production-requirement.prototype.iron", ProductionRequirementType.Material, material: iron, quantity: 2f);
            ProductionContextData productionContext = new ProductionContextData
            {
                actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                capabilityIds = new[] { "capability.production.blacksmithing" },
                knownFactDefinitionIds = new[] { "fact.prototype.blade-pattern" },
                resourceQuantities = { ProductionQuantity("resource.production.heat", 5f, sourceContainerId: "station.prototype.forge-heat", locationId: "location.prototype.smithy", revision: 5L) },
                itemQuantities = { ProductionQuantity(sword.Id, 1f, itemInstanceId: context.ScenarioContext.ScopedId("item-instance", "blank"), sourceContainerId: "container.prototype.smithy", locationId: "location.prototype.smithy", revision: 6L, stackRevision: 7L) },
                materialQuantities = { ProductionQuantity(iron.Id, 2f, itemInstanceId: context.ScenarioContext.ScopedId("item-instance", "iron-stack"), sourceContainerId: "container.prototype.smithy", locationId: "location.prototype.smithy", revision: 8L, stackRevision: 9L) }
            };

            ProductionRequirementEvaluationResult preview = production.EvaluateRequirements(new[] { skill, knowledge, heat, item, material }, productionContext, registry, itemRuntime, preview: true);
            bool valid = preview.Succeeded && preview.Preview && production.PlanCount == 0 && preview.Plan.selections.Count == 5 && preview.Plan.selections.SelectMany(selection => selection.allocations).Count() >= 3;
            return valid
                ? Pass(context, "step9-production-requirements", $"Selections={preview.Plan.selections.Count} Preview={preview.Preview}")
                : Fail(context, "step9-production-requirements", $"{preview.Status}: {preview.Message}");
        }

        private static TestLabAutomationStepResult ProductionReservationsInvalidation(TestLabAutomationContext context)
        {
            if (!TryCreateProductionRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out ItemDefinition sword, out ProductionToolDefinition hammer, out _, out ProductionStationDefinition forge, out string failure))
            {
                return Fail(context, "step9-production-reservations", failure);
            }

            ItemDurabilityRuntime durability = context.ScenarioContext.Runtimes.ItemDurability;
            string hammerItem = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "production-reserved-hammer"), ownerPersonId: context.ScenarioContext.Runtimes.PersonId).Snapshot.ItemInstanceId;
            durability.EnsureDefaultDurability(itemRuntime, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, registry, hammerItem);
            production.RegisterStation(forge, context.ScenarioContext.ScopedId("station", "reservation-forge"), "location.prototype.smithy");
            ProductionRequirementDefinition toolRequirement = ProductionRequirement("production-requirement.prototype.hammer-reserve", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            MaterialDefinition iron = Material("material.prototype.production-reserved-iron", MaterialCategory.Metal, 7.8f, 0.8f, 0.8f);
            registry = ExtendRegistry(registry, iron);
            ProductionRequirementDefinition ironSeven = ProductionRequirement("production-requirement.prototype.iron-seven", ProductionRequirementType.Material, material: iron, quantity: 7f);
            ProductionRequirementDefinition ironFive = ProductionRequirement("production-requirement.prototype.iron-five", ProductionRequirementType.Material, material: iron, quantity: 5f);
            ProductionContextData productionContext = new ProductionContextData
            {
                actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                locationId = "location.prototype.smithy",
                toolCandidates = { ToolCandidate(hammerItem, hammer) },
                materialQuantities = { ProductionQuantity(iron.Id, 10f, itemInstanceId: context.ScenarioContext.ScopedId("item-instance", "reserved-iron-stack"), sourceContainerId: "container.prototype.smithy", locationId: "location.prototype.smithy", revision: 12L, stackRevision: 13L) }
            };

            ProductionRequirementEvaluationResult plan = production.EvaluateRequirements(new[] { toolRequirement }, productionContext, registry, itemRuntime, durability, productionJobId: context.ScenarioContext.ScopedId("production-job", "reserve"));
            ProductionRequirementEvaluationResult materialPlan = production.EvaluateRequirements(new[] { ironSeven }, productionContext, registry, itemRuntime, durability, productionJobId: context.ScenarioContext.ScopedId("production-job", "reserve-material"));
            ProductionReservationResult materialReserve = production.ReservePlan(materialPlan.Plan.planId, "10");
            ProductionRequirementEvaluationResult materialConflict = production.EvaluateRequirements(new[] { ironFive }, productionContext, registry, itemRuntime, durability, productionJobId: context.ScenarioContext.ScopedId("production-job", "reserve-material-conflict"));
            ProductionReservationResult reserve = production.ReservePlan(plan.Plan.planId, "10");
            ProductionRequirementEvaluationResult conflicted = production.EvaluateRequirements(new[] { toolRequirement }, productionContext, registry, itemRuntime, durability, productionJobId: context.ScenarioContext.ScopedId("production-job", "reserve-conflict"));
            production.ReleasePlanReservations(plan.Plan.planId);
            production.ReleasePlanReservations(materialPlan.Plan.planId);
            durability.ApplyDamage(itemRuntime, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, registry, hammerItem, 1f);
            ProductionRequirementEvaluationResult stale = production.ValidatePlanCurrent(plan.Plan.planId, itemRuntime, durability);
            bool valid = plan.Succeeded && materialPlan.Succeeded && materialReserve.Succeeded && !materialConflict.Succeeded && reserve.Succeeded && !conflicted.Succeeded && stale.Status == ProductionRequirementEvaluationStatus.StalePlan;
            return valid
                ? Pass(context, "step9-production-reservations", $"Plan={plan.Plan.planId} Conflict={conflicted.Status} MaterialConflict={materialConflict.Status} Stale={stale.Status}")
                : Fail(context, "step9-production-reservations", $"Plan={plan.Status} Material={materialPlan.Status}/{materialReserve.Status}/{materialConflict.Status} Reserve={reserve.Status} Conflict={conflicted.Status} Stale={stale.Status}");
        }

        private static TestLabAutomationStepResult ProductionWearPersistence(TestLabAutomationContext context)
        {
            if (!TryCreateProductionRuntime(context, out ItemInstanceIdentityRuntime itemRuntime, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out ItemDefinition sword, out ProductionToolDefinition hammer, out _, out _, out string failure))
            {
                return Fail(context, "step9-production-wear-save", failure);
            }

            ItemDurabilityRuntime durability = context.ScenarioContext.Runtimes.ItemDurability;
            string hammerItem = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, "production-wear-hammer"), ownerPersonId: context.ScenarioContext.Runtimes.PersonId).Snapshot.ItemInstanceId;
            durability.EnsureDefaultDurability(itemRuntime, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, registry, hammerItem);
            ProductionRequirementDefinition toolRequirement = ProductionRequirement("production-requirement.prototype.hammer-wear", ProductionRequirementType.Tool, tool: hammer, role: ProductionToolRole.Primary);
            ProductionRequirementEvaluationResult plan = production.EvaluateRequirements(
                new[] { toolRequirement },
                new ProductionContextData { actorPersonId = context.ScenarioContext.Runtimes.PersonId, toolCandidates = { ToolCandidate(hammerItem, hammer) } },
                registry,
                itemRuntime,
                durability,
                productionJobId: context.ScenarioContext.ScopedId("production-job", "wear"));
            float beforeWear = durability.TryGetDurabilityForItem(hammerItem, out ItemDurabilitySnapshot before) ? before.NormalizedDurability : -1f;
            ProductionRequirementEvaluationResult wear = production.ApplyToolWearForPlan(plan.Plan.planId, itemRuntime, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, durability, registry);
            float afterWear = durability.TryGetDurabilityForItem(hammerItem, out ItemDurabilitySnapshot after) ? after.NormalizedDurability : -2f;
            ProductionReservationResult reserve = production.ReservePlan(plan.Plan.planId);
            ProductionRequirementRuntimeSaveData save = production.CreateSaveData();
            ProductionRequirementRuntime restored = new ProductionRequirementRuntime();
            ProductionRequirementEvaluationResult restore = restored.RestoreFromSaveData(save);
            bool valid = wear.Succeeded && Math.Abs(beforeWear - afterWear) < 0.0001f && plan.Plan.selections.Any(selection => selection.expectedToolWear > 0f) && reserve.Succeeded && restore.Succeeded && restored.PlanCount == 1 && restored.ReservationCount == 1;
            return valid
                ? Pass(context, "step9-production-wear-save", $"Plans={restored.PlanCount} Reservations={restored.ReservationCount} Wear={beforeWear:0.###}->{afterWear:0.###}")
                : Fail(context, "step9-production-wear-save", $"Plan={plan.Status} Wear={wear.Status} Durable={beforeWear}->{afterWear} Reserve={reserve.Status} Restore={restore.Status}");
        }

        private static TestLabAutomationStepResult RecipeDefinitionResolution(TestLabAutomationContext context)
        {
            if (!TryCreateRecipeRuntime(context, out _, out _, out DefinitionRegistry registry, out RecipeDefinition recipe, out _, out string failure))
            {
                return Fail(context, "step9-recipes-definition", failure);
            }

            DefinitionValidationReport report = new DefinitionValidationReport();
            _ = new DefinitionRegistry(registry.DefinitionsById.Values, report);
            bool valid = report.ErrorCount == 0 && recipe.Versions.Count >= 2 && recipe.Variants.Count == 1 && recipe.ProcedureSteps.Count == 3;
            return valid
                ? Pass(context, "step9-recipes-definition", $"Recipe={recipe.Id} Versions={recipe.Versions.Count} Variants={recipe.Variants.Count} Steps={recipe.ProcedureSteps.Count}")
                : Fail(context, "step9-recipes-definition", string.Join(" | ", report.Messages.Select(message => message.Message)));
        }

        private static TestLabAutomationStepResult RecipePreviewReservation(TestLabAutomationContext context)
        {
            if (!TryCreateRecipeRuntime(context, out RecipeRuntime runtime, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-recipes-preview", failure);
            }

            ProductionContextData productionContext = new ProductionContextData
            {
                actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                locationId = "location.prototype.smithy",
                materialQuantities =
                {
                    new ProductionQuantityData { definitionId = iron.Id, sourceContainerId = "container.prototype.materials", quantity = 6f, sourceTotalQuantity = 6f, unit = ProductionQuantityUnit.Kilogram }
                }
            };
            RecipeResolutionResult preview = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = recipe.Id,
                productionContext = productionContext,
                reservePlan = false
            }, registry, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemDurability);
            int plansAfterPreview = production.PlanCount;
            RecipeResolutionResult reserve = runtime.Resolve(new RecipeResolutionRequest
            {
                recipeId = recipe.Id,
                productionContext = productionContext,
                reservePlan = true,
                productionJobId = context.ScenarioContext.ScopedId("recipe-job", "sword")
            }, registry, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemDurability);

            bool valid = preview.Succeeded && preview.Preview && plansAfterPreview == 0 && reserve.Succeeded && !reserve.Preview && production.PlanCount == 1 && production.ReservationCount >= 1;
            return valid
                ? Pass(context, "step9-recipes-preview", $"Preview={preview.Status} Reserve={reserve.Status} Plans={production.PlanCount} Reservations={production.ReservationCount}")
                : Fail(context, "step9-recipes-preview", $"Preview={preview.Status} {preview.Message} Reserve={reserve.Status} {reserve.Message} Plans={production.PlanCount}");
        }

        private static TestLabAutomationStepResult RecipeKnowledgeProjection(TestLabAutomationContext context)
        {
            if (!TryCreateRecipeRuntime(context, out RecipeRuntime runtime, out _, out DefinitionRegistry registry, out RecipeDefinition recipe, out _, out string failure))
            {
                return Fail(context, "step9-recipes-knowledge", failure);
            }

            RecipeKnowledgeRuntime knowledge = context.ScenarioContext.Runtimes.RecipeKnowledge;
            RecipeResolvedSnapshot truth = runtime.Resolve(new RecipeResolutionRequest { recipeId = recipe.Id, buildRequirementPlan = false }, registry).Snapshot;
            RecipeKnowledgeRecordData record = knowledge.LearnOrUpdate(new RecipeKnowledgeRecordData
            {
                recordId = context.ScenarioContext.ScopedId("recipe-knowledge", "sword"),
                personId = context.ScenarioContext.Runtimes.PersonId,
                recipeId = recipe.Id,
                versionId = "recipe-version.prototype.sword.v1",
                completeness = RecipeKnowledgeCompleteness.Partial,
                knownInputIds = new[] { "recipe-input.prototype.iron" },
                knownOutputIds = new[] { "recipe-output.prototype.sword" },
                knownStepIds = new[] { "recipe-step.prototype.prepare" },
                sourceIds = new[] { "information-source.prototype.recipe-manual" }
            });
            RecipeResolvedSnapshot partial = knowledge.ProjectKnownRecipe(truth, record, RecipeProjectionAccessLevel.Ordinary);
            RecipeResolvedSnapshot privileged = knowledge.ProjectKnownRecipe(truth, record, RecipeProjectionAccessLevel.Privileged);
            bool valid = partial != null && privileged != null && partial.Redacted && partial.Inputs.Count < privileged.Inputs.Count && knowledge.RecordCount == 1;
            return valid
                ? Pass(context, "step9-recipes-knowledge", $"PartialInputs={partial.Inputs.Count} PrivilegedInputs={privileged.Inputs.Count} Records={knowledge.RecordCount}")
                : Fail(context, "step9-recipes-knowledge", "Recipe knowledge projection mismatch.");
        }

        private static TestLabAutomationStepResult RecipeKnowledgePersistence(TestLabAutomationContext context)
        {
            if (!TryCreateRecipeRuntime(context, out _, out _, out DefinitionRegistry registry, out RecipeDefinition recipe, out _, out string failure))
            {
                return Fail(context, "step9-recipes-persistence", failure);
            }

            RecipeKnowledgeRuntime knowledge = context.ScenarioContext.Runtimes.RecipeKnowledge;
            RecipeKnowledgeRecordData record = knowledge.LearnOrUpdate(new RecipeKnowledgeRecordData
            {
                recordId = context.ScenarioContext.ScopedId("recipe-knowledge", "persist"),
                personId = context.ScenarioContext.Runtimes.PersonId,
                recipeId = recipe.Id,
                versionId = "recipe-version.prototype.sword.v1",
                completeness = RecipeKnowledgeCompleteness.Complete
            });
            RecipeKnowledgeSaveData save = knowledge.CreateSaveData();
            RecipeKnowledgeRuntime restored = new RecipeKnowledgeRuntime();
            bool restore = restored.RestoreFromSaveData(save, registry, out string restoreFailure);
            RecipeKnowledgePersistenceParticipant participant = new RecipeKnowledgePersistenceParticipant(knowledge, () => registry, context.ScenarioContext.Runtimes.PersonId);
            RecipeKnowledgeSaveData bad = save.Clone();
            bad.records[0].recipeId = "recipe.prototype.missing";
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(UnityEngine.JsonUtility.ToJson(bad), RecipeKnowledgePersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = record != null && restore && restored.RecordCount == 1 && !prepare.Succeeded && knowledge.RecordCount == 1;
            return valid
                ? Pass(context, "step9-recipes-persistence", $"Restore={restore} Reject={prepare.Succeeded} Records={knowledge.RecordCount}")
                : Fail(context, "step9-recipes-persistence", $"Restore={restore} {restoreFailure} Prepare={prepare.Message}");
        }

        private static TestLabAutomationStepResult CraftingPreviewReadonly(TestLabAutomationContext context)
        {
            if (!TryCreateCraftingRuntime(context, out CraftingExecutionRuntime crafting, out RecipeRuntime recipes, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-crafting-preview", failure);
            }

            int itemsBefore = context.ScenarioContext.Runtimes.ItemInstances.Count;
            int plansBefore = production.PlanCount;
            CraftingExecutionResult preview = crafting.Preview(CraftingRequest(context, recipe, iron, "preview"), registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemDurability);
            bool valid = preview.Succeeded
                && preview.Preview
                && preview.Operation != null
                && preview.Operation.outputs.Count == 0
                && crafting.OperationCount == 0
                && context.ScenarioContext.Runtimes.ItemInstances.Count == itemsBefore
                && production.PlanCount == plansBefore;
            return valid
                ? Pass(context, "step9-crafting-preview", $"Preview={preview.Status} Items={itemsBefore}->{context.ScenarioContext.Runtimes.ItemInstances.Count} Plans={plansBefore}->{production.PlanCount}")
                : Fail(context, "step9-crafting-preview", $"Preview={preview.Status}:{preview.Message} Operations={crafting.OperationCount} Items={itemsBefore}->{context.ScenarioContext.Runtimes.ItemInstances.Count} Plans={plansBefore}->{production.PlanCount}");
        }

        private static TestLabAutomationStepResult CraftingExecuteOutputGraph(TestLabAutomationContext context)
        {
            if (!TryCreateCraftingRuntime(context, out CraftingExecutionRuntime crafting, out RecipeRuntime recipes, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-crafting-execute", failure);
            }

            CraftingExecutionResult result = crafting.Execute(CraftingRequest(context, recipe, iron, "execute"), registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, context.ScenarioContext.Runtimes.ItemDurability);
            string outputItem = result.Operation?.outputs.FirstOrDefault(output => output.createdItemInstance)?.itemInstanceId ?? string.Empty;
            bool valid = result.Succeeded
                && !result.Preview
                && result.Operation.state == CraftingOperationState.Completed
                && context.ScenarioContext.Runtimes.ItemInstances.TryGetSnapshot(outputItem, out _)
                && context.ScenarioContext.Runtimes.ItemCompositions.TryGetSnapshotForItem(outputItem, out _)
                && context.ScenarioContext.Runtimes.ItemQualityAffixes.TryGetQualityForItem(outputItem, out _)
                && context.ScenarioContext.Runtimes.ItemDurability.TryGetDurabilityForItem(outputItem, out _)
                && production.Plans.Any(plan => plan.status == ProductionPlanStatus.Released);
            return valid
                ? Pass(context, "step9-crafting-execute", $"Output={outputItem} Operations={crafting.OperationCount} Plans={production.PlanCount} Reservations={production.ReservationCount}")
                : Fail(context, "step9-crafting-execute", $"Result={result.Status}:{result.Message} Output={outputItem} Operations={crafting.OperationCount}");
        }

        private static TestLabAutomationStepResult CraftingDuplicateIdempotent(TestLabAutomationContext context)
        {
            if (!TryCreateCraftingRuntime(context, out CraftingExecutionRuntime crafting, out RecipeRuntime recipes, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-crafting-duplicate", failure);
            }

            CraftingExecutionRequest request = CraftingRequest(context, recipe, iron, "duplicate");
            CraftingExecutionResult first = crafting.Execute(request, registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, context.ScenarioContext.Runtimes.ItemDurability);
            int itemsAfterFirst = context.ScenarioContext.Runtimes.ItemInstances.Count;
            CraftingExecutionResult duplicate = crafting.Execute(request, registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, context.ScenarioContext.Runtimes.ItemDurability);
            bool valid = first.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && context.ScenarioContext.Runtimes.ItemInstances.Count == itemsAfterFirst
                && duplicate.Operation.outputs.Select(output => output.itemInstanceId).SequenceEqual(first.Operation.outputs.Select(output => output.itemInstanceId));
            return valid
                ? Pass(context, "step9-crafting-duplicate", $"First={first.Status} Duplicate={duplicate.Status} Items={itemsAfterFirst}->{context.ScenarioContext.Runtimes.ItemInstances.Count}")
                : Fail(context, "step9-crafting-duplicate", $"First={first.Status}:{first.Message} Duplicate={duplicate.Status}:{duplicate.Message} Items={itemsAfterFirst}->{context.ScenarioContext.Runtimes.ItemInstances.Count}");
        }

        private static TestLabAutomationStepResult CraftingFailureRollback(TestLabAutomationContext context)
        {
            if (!TryCreateCraftingRuntime(context, out CraftingExecutionRuntime crafting, out RecipeRuntime recipes, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-crafting-rollback", failure);
            }

            RecipeDefinition broken = PrototypeRecipeWithMissingOutput(recipe, iron);
            registry = ExtendRegistry(registry, broken);
            int itemsBefore = context.ScenarioContext.Runtimes.ItemInstances.Count;
            int plansBefore = production.PlanCount;
            CraftingExecutionRequest request = CraftingRequest(context, broken, iron, "rollback");
            CraftingExecutionResult result = crafting.Execute(request, registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, context.ScenarioContext.Runtimes.ItemDurability);
            bool valid = !result.Succeeded
                && result.Status == CraftingExecutionStatus.OutputCreationFailed
                && context.ScenarioContext.Runtimes.ItemInstances.Count == itemsBefore
                && production.PlanCount == plansBefore
                && crafting.OperationCount == 0;
            return valid
                ? Pass(context, "step9-crafting-rollback", $"Failure={result.Status} Items={itemsBefore}->{context.ScenarioContext.Runtimes.ItemInstances.Count} Plans={plansBefore}->{production.PlanCount}")
                : Fail(context, "step9-crafting-rollback", $"Failure={result.Status}:{result.Message} Items={itemsBefore}->{context.ScenarioContext.Runtimes.ItemInstances.Count} Plans={plansBefore}->{production.PlanCount} Operations={crafting.OperationCount}");
        }

        private static TestLabAutomationStepResult CraftingPersistence(TestLabAutomationContext context)
        {
            if (!TryCreateCraftingRuntime(context, out CraftingExecutionRuntime crafting, out RecipeRuntime recipes, out ProductionRequirementRuntime production, out DefinitionRegistry registry, out RecipeDefinition recipe, out MaterialDefinition iron, out string failure))
            {
                return Fail(context, "step9-crafting-persistence", failure);
            }

            CraftingExecutionResult result = crafting.Execute(CraftingRequest(context, recipe, iron, "persist"), registry, recipes, production, context.ScenarioContext.Runtimes.ItemInstances, context.ScenarioContext.Runtimes.ItemCompositions, context.ScenarioContext.Runtimes.ItemQualityAffixes, context.ScenarioContext.Runtimes.ItemDurability);
            CraftingExecutionRuntimeSaveData save = crafting.CreateSaveData();
            CraftingExecutionRuntime restored = new CraftingExecutionRuntime();
            CraftingExecutionResult restore = restored.RestoreFromSaveData(save, registry);
            CraftingExecutionPersistenceParticipant participant = new CraftingExecutionPersistenceParticipant(crafting, () => registry, context.ScenarioContext.Runtimes.WorldId);
            CraftingExecutionRuntimeSaveData bad = save.Clone();
            bad.operations[0].recipeId = "recipe.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(UnityEngine.JsonUtility.ToJson(bad), CraftingExecutionPersistenceParticipant.CurrentParticipantSchemaVersion);
            bool valid = result.Succeeded
                && restore.Succeeded
                && restored.OperationCount == 1
                && !rejected.Succeeded
                && crafting.OperationCount == 1;
            return valid
                ? Pass(context, "step9-crafting-persistence", $"Restore={restore.Status} Reject={rejected.Succeeded} Operations={restored.OperationCount}")
                : Fail(context, "step9-crafting-persistence", $"Execute={result.Status}:{result.Message} Restore={restore.Status}:{restore.Message} Reject={rejected.Message}");
        }

        private static TestLabAutomationStepResult ProductionChainValidation(TestLabAutomationContext context)
        {
            if (!TryProductionWorkflowFixture(context, out _, out DefinitionRegistry registry, out ProductionChainDefinition chain, out _, out string failure))
            {
                return Fail(context, "step9-production-chain-validate", failure);
            }

            DefinitionValidationReport validReport = new DefinitionValidationReport();
            chain.ValidateCatalogDefinition(registry.DefinitionsById, validReport);
            ProductionChainVersionData snapshot = chain.Versions.Single(version => version.versionId == "production-chain-version.prototype.sword.v1");
            string originalStage = snapshot.stages[0].stageId;
            snapshot.stages[0].stageId = "mutated";
            bool immutable = chain.Versions.Single(version => version.versionId == "production-chain-version.prototype.sword.v1").stages[0].stageId == originalStage;

            ProductionChainDefinition cyclic = PrototypeProductionChain("production-chain.prototype.cyclic", "production-chain-version.prototype.cyclic.v1", "recipe.prototype.sword", cyclic: true);
            DefinitionValidationReport cyclicReport = new DefinitionValidationReport();
            Dictionary<string, IGameDefinition> cyclicDefinitions = registry.DefinitionsById.Values
                .Where(definition => definition != null && !string.Equals(definition.Id, cyclic.Id, StringComparison.Ordinal))
                .Concat(new IGameDefinition[] { cyclic })
                .ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal);
            cyclic.ValidateCatalogDefinition(cyclicDefinitions, cyclicReport);

            bool valid = validReport.ErrorCount == 0 && cyclicReport.ErrorCount > 0 && immutable;
            return valid
                ? Pass(context, "step9-production-chain-validate", $"ValidErrors={validReport.ErrorCount} CycleErrors={cyclicReport.ErrorCount} Immutable={immutable}")
                : Fail(context, "step9-production-chain-validate", $"ValidErrors={validReport.ErrorCount} CycleErrors={cyclicReport.ErrorCount} Immutable={immutable}");
        }

        private static TestLabAutomationStepResult ProductionWorkOrderJobQueue(TestLabAutomationContext context)
        {
            if (!TryProductionWorkflowFixture(context, out ProductionWorkflowRuntime workflow, out DefinitionRegistry registry, out ProductionChainDefinition chain, out _, out string failure))
            {
                return Fail(context, "step9-production-work-order", failure);
            }

            string queueId = $"production-queue.test.{RunGuid(context, "queue")}";
            ProductionWorkflowResult queue = workflow.EnsureQueue(queueId);
            ProductionWorkflowResult lowOrder = workflow.CreateWorkOrder(WorkOrder(context, "low", chain.Id, chain.CurrentVersionId, priority: 1), registry);
            ProductionWorkflowResult highOrder = workflow.CreateWorkOrder(WorkOrder(context, "high", chain.Id, chain.CurrentVersionId, priority: 10), registry);
            workflow.TransitionWorkOrder(lowOrder.WorkOrder.workOrderId, ProductionWorkOrderState.Approved);
            workflow.TransitionWorkOrder(highOrder.WorkOrder.workOrderId, ProductionWorkOrderState.Approved);
            ProductionWorkflowResult lowJob = workflow.CreateJobFromWorkOrder($"production-job.test.{RunGuid(context, "low-job")}", lowOrder.WorkOrder.workOrderId, registry, queueId);
            ProductionWorkflowResult highJob = workflow.CreateJobFromWorkOrder($"production-job.test.{RunGuid(context, "high-job")}", highOrder.WorkOrder.workOrderId, registry, queueId);

            ProductionQueueData queued = workflow.Queues.Single(entry => entry.queueId == queueId);
            bool valid = queue.Succeeded
                && lowOrder.Succeeded
                && highOrder.Succeeded
                && lowJob.Succeeded
                && highJob.Succeeded
                && queued.jobIds.Count == 2
                && queued.jobIds.First() == highJob.Job.jobId
                && workflow.BatchCount == 2;

            return valid
                ? Pass(context, "step9-production-work-order", $"Queue={queueId} First={queued.jobIds.First()} Jobs={workflow.JobCount} Batches={workflow.BatchCount}")
                : Fail(context, "step9-production-work-order", $"Queue={queue.Status} Low={lowJob.Status}:{lowJob.Job?.priority} High={highJob.Status}:{highJob.Job?.priority} Order=[{string.Join(",", queued.jobIds)}] Priorities=[{string.Join(",", workflow.Jobs.Select(job => $"{job.jobId}:{job.priority}"))}] ExpectedFirst={highJob.Job?.jobId} Jobs={workflow.JobCount} Batches={workflow.BatchCount}");
        }

        private static TestLabAutomationStepResult ProductionProgressIdempotent(TestLabAutomationContext context)
        {
            if (!TryStartedProductionJob(context, out ProductionWorkflowRuntime workflow, out _, out string jobId, out string stageId, out string failure))
            {
                return Fail(context, "step9-production-progress", failure);
            }

            ProductionWorkflowResult first = workflow.EvaluateJobToWorldTime(jobId, "1");
            long revAfterFirst = workflow.Revision;
            ProductionWorkflowResult duplicate = workflow.EvaluateJobToWorldTime(jobId, "1");
            long revAfterDuplicate = workflow.Revision;
            ProductionWorkflowResult second = workflow.EvaluateJobToWorldTime(jobId, "2");
            workflow.TryGetJob(jobId, out ProductionJobData job);
            ProductionStageProgressData stage = job.stages.Single(value => value.stageId == stageId);

            bool valid = first.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && second.Succeeded
                && Math.Abs(stage.completedWork - stage.requiredWork) < 0.0001f
                && stage.state == ProductionStageRuntimeState.ReadyToComplete
                && revAfterFirst == revAfterDuplicate;
            return valid
                ? Pass(context, "step9-production-progress", $"Progress={stage.completedWork}/{stage.requiredWork} Rev={revAfterFirst}->{revAfterDuplicate}->{workflow.Revision}")
                : Fail(context, "step9-production-progress", $"First={first.Status} Duplicate={duplicate.Status} Second={second.Status} State={stage.state}");
        }

        private static TestLabAutomationStepResult ProductionStageOutputLineage(TestLabAutomationContext context)
        {
            if (!TryStartedProductionJob(context, out ProductionWorkflowRuntime workflow, out DefinitionRegistry registry, out string jobId, out string stageId, out string failure))
            {
                return Fail(context, "step9-production-lineage", failure);
            }

            TestLabRuntimeBundle runtimes = context.ScenarioContext.Runtimes;
            workflow.EvaluateJobToWorldTime(jobId, "2");
            ProductionWorkflowResult complete = workflow.CompleteStage(jobId, stageId, registry, new RecipeRuntime(), runtimes.ProductionRequirements, runtimes.ItemInstances, runtimes.ItemCompositions, runtimes.ItemQualityAffixes, runtimes.ItemDurability, runtimes.CraftingExecution, ProductionContext(context, "lineage"), "2");
            ProductionWorkflowResult duplicate = workflow.CompleteStage(jobId, stageId, registry, new RecipeRuntime(), runtimes.ProductionRequirements, runtimes.ItemInstances, runtimes.ItemCompositions, runtimes.ItemQualityAffixes, runtimes.ItemDurability, runtimes.CraftingExecution, ProductionContext(context, "lineage"), "2");
            workflow.TryGetJob(jobId, out ProductionJobData job);

            bool valid = complete.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && workflow.BatchCount == 1
                && workflow.LotCount == 1
                && workflow.IntermediateCount == 1
                && job.outputItemIds.Length > 0
                && runtimes.ItemInstances.TryGetSnapshot(job.outputItemIds[0], out _);

            return valid
                ? Pass(context, "step9-production-lineage", $"Batch={job.batchId} Lots={workflow.LotCount} Intermediates={workflow.IntermediateCount} Outputs={job.outputItemIds.Length}")
                : Fail(context, "step9-production-lineage", $"Complete={complete.Status} Duplicate={duplicate.Status} Batches={workflow.BatchCount} Lots={workflow.LotCount} Intermediates={workflow.IntermediateCount}");
        }

        private static TestLabAutomationStepResult ProductionLifecycleBoundaries(TestLabAutomationContext context)
        {
            if (!TryStartedProductionJob(context, out ProductionWorkflowRuntime workflow, out _, out string jobId, out string stageId, out string failure))
            {
                return Fail(context, "step9-production-lifecycle", failure);
            }

            ProductionWorkflowResult pause = workflow.PauseJob(jobId, "manual", "1");
            long pausedRevision = workflow.Revision;
            ProductionWorkflowResult pausedProgress = workflow.EvaluateJobToWorldTime(jobId, "4");
            long afterPausedProgress = workflow.Revision;
            ProductionWorkflowResult resume = workflow.ResumeJob(jobId, "4");
            ProductionWorkflowResult interrupt = workflow.InterruptJob(jobId, "tool-break", "5");
            ProductionWorkflowResult recover = workflow.ResumeJob(jobId, "6");
            ProductionWorkflowResult cancel = workflow.CancelJob(jobId, "user-cancelled", "7");
            ProductionWorkflowResult duplicateCancel = workflow.CancelJob(jobId, "user-cancelled", "7");
            workflow.TryGetJob(jobId, out ProductionJobData job);

            bool valid = pause.Succeeded
                && pausedProgress.Succeeded
                && pausedRevision == afterPausedProgress
                && resume.Succeeded
                && interrupt.Succeeded
                && recover.Succeeded
                && cancel.Succeeded
                && duplicateCancel.Succeeded
                && duplicateCancel.Duplicate
                && job.state == ProductionJobState.Cancelled
                && job.stages.Single(stage => stage.stageId == stageId).state == ProductionStageRuntimeState.Cancelled;
            return valid
                ? Pass(context, "step9-production-lifecycle", $"State={job.state} PauseRev={pausedRevision}->{afterPausedProgress}")
                : Fail(context, "step9-production-lifecycle", $"Pause={pause.Status} Resume={resume.Status} Interrupt={interrupt.Status} Recover={recover.Status} Cancel={cancel.Status}");
        }

        private static TestLabAutomationStepResult ProductionPersistenceProjection(TestLabAutomationContext context)
        {
            if (!TryStartedProductionJob(context, out ProductionWorkflowRuntime workflow, out DefinitionRegistry registry, out string jobId, out _, out string failure))
            {
                return Fail(context, "step9-production-save-project", failure);
            }

            ProductionWorkflowRuntimeSaveData save = workflow.CreateSaveData();
            ProductionWorkflowRuntime restored = new ProductionWorkflowRuntime();
            ProductionWorkflowResult restore = restored.RestoreFromSaveData(save, registry);
            ProductionProjectionData publicProjection = restored.ProjectJob(jobId, ProductionProjectionAudience.PublicObserver);
            ProductionProjectionData privilegedProjection = restored.ProjectJob(jobId, ProductionProjectionAudience.PrivilegedDebug);
            ProductionWorkflowRuntimeSaveData corrupt = save.Clone();
            corrupt.jobs[0].batchId = "production-batch.missing";
            ProductionWorkflowResult rejected = restored.RestoreFromSaveData(corrupt, registry);

            bool valid = restore.Succeeded
                && restored.JobCount == workflow.JobCount
                && restored.Revision == workflow.Revision
                && publicProjection.Decision == ProductionProjectionDecision.RedactedAccess
                && privilegedProjection.Decision == ProductionProjectionDecision.FullAccess
                && !rejected.Succeeded
                && restored.JobCount == workflow.JobCount;
            return valid
                ? Pass(context, "step9-production-save-project", $"Restore={restore.Status} Public={publicProjection.Decision} Privileged={privilegedProjection.Decision}")
                : Fail(context, "step9-production-save-project", $"Restore={restore.Status} Public={publicProjection.Decision} Rejected={rejected.Status}");
        }

        private static bool TryCreateRuntime(TestLabAutomationContext context, out ItemInstanceIdentityRuntime runtime, out ItemDefinition sword, out string failure)
        {
            runtime = context?.ScenarioContext?.Runtimes?.ItemInstances;
            sword = null;
            failure = string.Empty;
            if (runtime == null)
            {
                failure = "Item instance runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (registry == null)
            {
                failure = "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(SwordId, out sword))
            {
                failure = $"Item definition '{SwordId}' is missing.";
                return false;
            }

            return true;
        }

        private static bool TryCreateRecipeRuntime(
            TestLabAutomationContext context,
            out RecipeRuntime recipeRuntime,
            out ProductionRequirementRuntime production,
            out DefinitionRegistry registry,
            out RecipeDefinition recipe,
            out MaterialDefinition iron,
            out string failure)
        {
            recipeRuntime = new RecipeRuntime();
            production = context?.ScenarioContext?.Runtimes?.ProductionRequirements;
            registry = null;
            recipe = null;
            iron = null;
            failure = string.Empty;

            if (production == null || context?.ScenarioContext?.Runtimes?.RecipeKnowledge == null)
            {
                failure = "Recipe automation requires production and recipe knowledge runtimes from the Test Lab runtime bundle.";
                return false;
            }

            DefinitionRegistry existing = context.ScenarioContext.Runtimes.DefinitionRegistry;
            if (existing == null || !existing.TryGet(SwordId, out ItemDefinition sword))
            {
                failure = $"Item definition '{SwordId}' is missing.";
                return false;
            }

            iron = Material("material.prototype.recipe-iron", MaterialCategory.Metal, 7.8f, 0.8f, 0.8f);
            recipe = PrototypeRecipe(sword, iron);
            registry = ExtendRegistry(existing, iron, recipe);
            return true;
        }

        private static bool TryCreateCraftingRuntime(
            TestLabAutomationContext context,
            out CraftingExecutionRuntime crafting,
            out RecipeRuntime recipeRuntime,
            out ProductionRequirementRuntime production,
            out DefinitionRegistry registry,
            out RecipeDefinition recipe,
            out MaterialDefinition iron,
            out string failure)
        {
            crafting = context?.ScenarioContext?.Runtimes?.CraftingExecution;
            if (crafting == null)
            {
                recipeRuntime = null;
                production = null;
                registry = null;
                recipe = null;
                iron = null;
                failure = "Crafting execution runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            return TryCreateRecipeRuntime(context, out recipeRuntime, out production, out registry, out recipe, out iron, out failure);
        }

        private static bool TryProductionWorkflowFixture(
            TestLabAutomationContext context,
            out ProductionWorkflowRuntime workflow,
            out DefinitionRegistry registry,
            out ProductionChainDefinition chain,
            out RecipeDefinition recipe,
            out string failure)
        {
            workflow = context?.ScenarioContext?.Runtimes?.ProductionWorkflow;
            chain = null;
            recipe = null;
            if (workflow == null)
            {
                registry = null;
                failure = "Production workflow runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (!TryCreateCraftingRuntime(context, out _, out _, out _, out registry, out recipe, out _, out failure))
            {
                return false;
            }

            chain = PrototypeProductionChain("production-chain.prototype.sword", "production-chain-version.prototype.sword.v1", recipe.Id);
            registry = ExtendRegistry(registry, chain);
            return true;
        }

        private static bool TryStartedProductionJob(
            TestLabAutomationContext context,
            out ProductionWorkflowRuntime workflow,
            out DefinitionRegistry registry,
            out string jobId,
            out string stageId,
            out string failure)
        {
            jobId = string.Empty;
            stageId = string.Empty;
            if (!TryProductionWorkflowFixture(context, out workflow, out registry, out ProductionChainDefinition chain, out _, out failure))
            {
                return false;
            }

            ProductionWorkOrderData order = WorkOrder(context, "started", chain.Id, chain.CurrentVersionId, priority: 5);
            ProductionWorkflowResult created = workflow.CreateWorkOrder(order, registry);
            if (!created.Succeeded)
            {
                failure = created.Message;
                return false;
            }

            workflow.TransitionWorkOrder(order.workOrderId, ProductionWorkOrderState.Approved);
            jobId = $"production-job.test.{RunGuid(context, "started-job")}";
            ProductionWorkflowResult job = workflow.CreateJobFromWorkOrder(jobId, order.workOrderId, registry);
            if (!job.Succeeded)
            {
                failure = job.Message;
                return false;
            }

            stageId = job.Job.readyStageIds.FirstOrDefault() ?? job.Job.currentStageId;
            ProductionWorkflowResult start = workflow.StartStage(jobId, stageId, context.ScenarioContext.Runtimes.ProductionRequirements, registry, ProductionContext(context, "started"), "station.prototype.workflow", 1, "0");
            if (!start.Succeeded)
            {
                failure = start.Message;
                return false;
            }

            return true;
        }

        private static ProductionWorkOrderData WorkOrder(TestLabAutomationContext context, string slug, string chainId, string versionId, int priority)
        {
            return new ProductionWorkOrderData
            {
                workOrderId = $"production-work-order.test.{RunGuid(context, slug)}",
                requesterPersonId = context?.ScenarioContext?.Runtimes?.PersonId ?? "person.prototype.crafter",
                chainDefinitionId = chainId,
                versionId = versionId,
                requestedQuantity = 1,
                priority = priority,
                ownerPersonId = context?.ScenarioContext?.Runtimes?.PersonId ?? "person.prototype.crafter",
                custodianPersonId = context?.ScenarioContext?.Runtimes?.PersonId ?? "person.prototype.crafter",
                earliestStartWorldTime = "0",
                destinationId = "container.prototype.output",
                provenance = $"testlab={context?.RunId}"
            };
        }

        private static ProductionContextData ProductionContext(TestLabAutomationContext context, string slug)
        {
            return new ProductionContextData
            {
                actorPersonId = context?.ScenarioContext?.Runtimes?.PersonId ?? "person.prototype.crafter",
                locationId = "location.prototype.production",
                worldTime = "0",
                materialQuantities =
                {
                    new ProductionQuantityData
                    {
                        definitionId = "material.prototype.recipe-iron",
                        sourceContainerId = $"container.production.{RunGuid(context, slug)}",
                        quantity = 20f,
                        sourceTotalQuantity = 20f,
                        unit = ProductionQuantityUnit.Kilogram
                    }
                }
            };
        }

        private static bool TryCreateProductionRuntime(
            TestLabAutomationContext context,
            out ItemInstanceIdentityRuntime itemRuntime,
            out ProductionRequirementRuntime production,
            out DefinitionRegistry registry,
            out ItemDefinition sword,
            out ProductionToolDefinition hammer,
            out ProductionToolDefinition mallet,
            out ProductionStationDefinition forge,
            out string failure)
        {
            production = context?.ScenarioContext?.Runtimes?.ProductionRequirements;
            itemRuntime = context?.ScenarioContext?.Runtimes?.ItemInstances;
            sword = null;
            hammer = null;
            mallet = null;
            forge = null;
            registry = null;
            failure = string.Empty;

            if (production == null || itemRuntime == null)
            {
                failure = "Production requirement or item identity runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            DefinitionRegistry existing = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (existing == null || !existing.TryGet(SwordId, out sword))
            {
                failure = $"Item definition '{SwordId}' is missing.";
                return false;
            }

            hammer = ProductionTool("production-tool.prototype.hammer", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" }, wear: 2f);
            mallet = ProductionTool("production-tool.prototype.mallet", ProductionToolCategory.Hammering, new[] { ProductionToolRole.Primary }, new[] { "tool.capability.strike" }, substitutesFor: new[] { hammer.Id }, priority: 10, wear: 1f);
            forge = ProductionStation("production-station.prototype.forge", ProductionStationCategory.Forge, new[] { "station.capability.heat" }, new[] { ProductionToolRole.Primary });
            registry = ExtendRegistry(existing, hammer, mallet, forge);
            return true;
        }

        private static DefinitionRegistry CreateCompositionRegistry(TestLabAutomationContext context, bool includeRule, out string failure, bool includeComposite = false)
        {
            failure = string.Empty;
            DefinitionRegistry existing = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (existing == null || !existing.TryGet(SwordId, out ItemDefinition sword))
            {
                failure = $"Item definition '{SwordId}' is missing.";
                return null;
            }

            MaterialDefinition iron = Material("material.prototype.iron", MaterialCategory.Metal, 7.8f, 0.8f, 0.75f);
            MaterialDefinition wood = Material("material.prototype.wood", MaterialCategory.Wood, 0.7f, 0.25f, 0.45f);
            MaterialDefinition oil = Material("material.prototype.oil", MaterialCategory.Liquid, 0.9f, 0.02f, 0.1f);
            MaterialDefinition steel = Material("material.prototype.steel", MaterialCategory.Composite, 7.7f, 0.9f, 0.9f);
            MaterialDefinition pattern = Material("material.prototype.pattern-weld", MaterialCategory.Composite, 7.75f, 0.9f, 0.95f);
            SetPrivate(steel, "constituents", new[] { Constituent(iron, 1f) });
            SetPrivate(pattern, "constituents", new[] { Constituent(steel, 0.5f), Constituent(iron, 0.5f) });
            System.Collections.Generic.List<IGameDefinition> definitions = existing.DefinitionsById.Values
                .Where(definition => definition != null && definition.Id != iron.Id && definition.Id != wood.Id && definition.Id != oil.Id && definition.Id != steel.Id && definition.Id != pattern.Id)
                .Concat(new IGameDefinition[] { iron, wood, oil, steel })
                .ToList();
            if (includeComposite)
            {
                definitions.Add(pattern);
            }
            if (includeRule)
            {
                MaterialCompatibilityRuleDefinition rule = UnityEngine.ScriptableObject.CreateInstance<MaterialCompatibilityRuleDefinition>();
                SetPrivate(rule, "ruleId", "material-rule.prototype.oil-on-iron");
                SetPrivate(rule, "displayName", "Oil on Iron");
                SetPrivate(rule, "sourceMaterial", iron);
                SetPrivate(rule, "targetMaterial", oil);
                SetPrivate(rule, "outcome", MaterialCompatibilityOutcome.Degrades);
                SetPrivate(rule, "priority", 100);
                definitions.Add(rule);
            }

            return new DefinitionRegistry(definitions);
        }

        private static bool TryCreateQualityRuntime(
            TestLabAutomationContext context,
            out ItemInstanceIdentityRuntime itemRuntime,
            out ItemCompositionRuntime compositions,
            out ItemQualityAffixRuntime quality,
            out DefinitionRegistry registry,
            out ItemDefinition sword,
            out QualityTierDefinition masterwork,
            out ItemAffixDefinition keen,
            out string failure)
        {
            itemRuntime = context?.ScenarioContext?.Runtimes?.ItemInstances;
            compositions = context?.ScenarioContext?.Runtimes?.ItemCompositions;
            quality = context?.ScenarioContext?.Runtimes?.ItemQualityAffixes;
            sword = null;
            masterwork = null;
            keen = null;
            registry = null;
            failure = string.Empty;

            if (itemRuntime == null || compositions == null || quality == null)
            {
                failure = "Item identity, composition, or quality runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            registry = CreateQualityRegistry(context, out failure, out masterwork, out keen);
            if (registry == null || !registry.TryGet(SwordId, out sword))
            {
                failure = string.IsNullOrWhiteSpace(failure) ? $"Item definition '{SwordId}' is missing." : failure;
                return false;
            }

            return true;
        }

        private static bool TryCreateDurabilityRuntime(
            TestLabAutomationContext context,
            out ItemInstanceIdentityRuntime itemRuntime,
            out ItemCompositionRuntime compositions,
            out ItemQualityAffixRuntime quality,
            out ItemDurabilityRuntime durability,
            out DefinitionRegistry registry,
            out ItemDefinition sword,
            out string failure)
        {
            itemRuntime = context?.ScenarioContext?.Runtimes?.ItemInstances;
            compositions = context?.ScenarioContext?.Runtimes?.ItemCompositions;
            quality = context?.ScenarioContext?.Runtimes?.ItemQualityAffixes;
            durability = context?.ScenarioContext?.Runtimes?.ItemDurability;
            sword = null;
            registry = null;
            failure = string.Empty;

            if (itemRuntime == null || compositions == null || quality == null || durability == null)
            {
                failure = "Item identity, composition, quality, or durability runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            registry = CreateQualityRegistry(context, out failure, out _, out _);
            if (registry == null || !registry.TryGet(SwordId, out sword))
            {
                failure = string.IsNullOrWhiteSpace(failure) ? $"Item definition '{SwordId}' is missing." : failure;
                return false;
            }

            return true;
        }

        private static DefinitionRegistry CreateQualityRegistry(TestLabAutomationContext context, out string failure, out QualityTierDefinition masterwork, out ItemAffixDefinition keen)
        {
            DefinitionRegistry compositionRegistry = CreateCompositionRegistry(context, includeRule: false, out failure, includeComposite: true);
            masterwork = QualityTier("quality.masterwork", "Masterwork", 0.85f, 0.98f, 80);
            QualityTierDefinition common = QualityTier("quality.common", "Common", 0.35f, 0.65f, 30);
            QualityTierDefinition fine = QualityTier("quality.fine", "Fine", 0.65f, 0.85f, 60);
            QualityTierDefinition legendary = QualityTier("quality.legendary-foundation", "Legendary Quality Foundation", 0.98f, 1f, 100);
            keen = Affix("affix.prototype.keen-edge", "Keen Edge", ItemAffixClassification.Prefix, "affix-tier.prototype.keen.fine", 0.55f, 1f, 1f, 1f, 0.08f, 2f, exclusiveGroup: "affix-group.edge-sharpness");
            ItemAffixDefinition precise = Affix("affix.prototype.precise", "Precise", ItemAffixClassification.Suffix, "affix-tier.prototype.precise.fine", 0.45f, 1f, 0.5f, 0.5f, 0.04f, 1f, exclusiveGroup: "affix-group.precision");

            List<IGameDefinition> definitions = compositionRegistry?.DefinitionsById.Values.ToList() ?? new List<IGameDefinition>();
            definitions.RemoveAll(definition => definition is QualityTierDefinition || definition is ItemAffixDefinition);
            definitions.AddRange(new IGameDefinition[] { common, fine, masterwork, legendary, keen, precise });
            return new DefinitionRegistry(definitions);
        }

        private static string CreateComposedItem(TestLabAutomationContext context, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositions, DefinitionRegistry registry, ItemDefinition sword, string slug)
        {
            string itemId = itemRuntime.CreateItem(sword, itemInstanceId: RunGuid(context, slug)).Snapshot.ItemInstanceId;
            compositions.SetComposition(itemRuntime, registry, Composition(itemId, "material.prototype.iron"));
            return itemId;
        }

        private static ItemQualityRecordData QualityRecord(string itemInstanceId, float quality)
        {
            return new ItemQualityRecordData
            {
                itemInstanceId = itemInstanceId,
                itemDefinitionId = SwordId,
                overallQuality = quality,
                source = ItemQualityRecordSource.TestLab,
                workmanship =
                {
                    Workmanship("workmanship.overall", WorkmanshipDimension.Overall, quality)
                },
                dimensions =
                {
                    Dimension("quality.functional", ItemQualityDimension.Functional, quality, 1f)
                }
            };
        }

        private static ItemWorkmanshipEntryData Workmanship(string id, WorkmanshipDimension dimension, float value)
        {
            return new ItemWorkmanshipEntryData
            {
                entryId = id,
                dimension = dimension,
                value = new ItemQualityValueData { state = QualityValueState.Known, value = value }
            };
        }

        private static ItemQualityDimensionEntryData Dimension(string id, ItemQualityDimension dimension, float value, float weight)
        {
            return new ItemQualityDimensionEntryData
            {
                entryId = id,
                dimension = dimension,
                value = new ItemQualityValueData { state = QualityValueState.Known, value = value },
                weight = weight
            };
        }

        private static QualityTierDefinition QualityTier(string id, string name, float min, float max, int order)
        {
            QualityTierDefinition tier = UnityEngine.ScriptableObject.CreateInstance<QualityTierDefinition>();
            SetPrivate(tier, "tierId", id);
            SetPrivate(tier, "displayName", name);
            SetPrivate(tier, "minimumQuality", min);
            SetPrivate(tier, "maximumQuality", max);
            SetPrivate(tier, "sortOrder", order);
            return tier;
        }

        private static ItemAffixDefinition Affix(string id, string name, ItemAffixClassification classification, string tierId, float minQuality, float maxQuality, float minValue, float maxValue, float rarityContribution, float modifierValue, string exclusiveGroup)
        {
            ItemAffixDefinition definition = UnityEngine.ScriptableObject.CreateInstance<ItemAffixDefinition>();
            SetPrivate(definition, "affixId", id);
            SetPrivate(definition, "displayName", name);
            SetPrivate(definition, "classification", classification);
            SetPrivate(definition, "maximumOccurrences", 1);
            SetPrivate(definition, "maximumPrefixCount", 3);
            SetPrivate(definition, "maximumSuffixCount", 3);
            SetPrivate(definition, "maximumTotalAffixCount", 6);
            SetPrivate(definition, "exclusiveGroups", new[] { exclusiveGroup });
            SetPrivate(definition, "rarityContribution", rarityContribution);
            SetPrivate(definition, "generationWeight", 1f);
            SetPrivate(definition, "tiers", new[]
            {
                new ItemAffixTierData
                {
                    tierId = tierId,
                    sortOrder = 10,
                    minimumItemQuality = minQuality,
                    maximumItemQuality = maxQuality,
                    valueMinimum = minValue,
                    valueMaximum = maxValue,
                    rarityContribution = rarityContribution,
                    modifierTemplates = new[] { StatModifier(StatType.AttackPower, StatModifierOperation.FlatAdd, modifierValue) }
                }
            });
            return definition;
        }

        private static StatModifierDefinition StatModifier(StatType statType, StatModifierOperation operation, float value)
        {
            StatModifierDefinition modifier = new StatModifierDefinition();
            SetPrivate(modifier, "statType", statType);
            SetPrivate(modifier, "operation", operation);
            SetPrivate(modifier, "value", value);
            SetPrivate(modifier, "scaleWithStacks", false);
            return modifier;
        }

        private static ItemCompositionRecordData Composition(string itemInstanceId, string materialId)
        {
            return new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{itemInstanceId}",
                itemInstanceId = itemInstanceId,
                sourceItemDefinitionId = SwordId,
                completeness = ItemCompositionCompleteness.Complete,
                source = "test-lab.automation",
                materials =
                {
                    MaterialEntry("entry.blade", materialId, MaterialEntryRole.PrimaryStructure, 1.2f, MaterialQuantityUnit.Kilogram)
                },
                components =
                {
                    new ItemComponentEntryData
                    {
                        componentEntryId = "component.blade",
                        kind = ItemComponentKind.AbstractComponent,
                        materialEntryIds = new[] { "entry.blade" }
                    }
                }
            };
        }

        private static ItemMaterialEntryData MaterialEntry(string entryId, string materialId, MaterialEntryRole role, float value, MaterialQuantityUnit unit)
        {
            return new ItemMaterialEntryData
            {
                entryId = entryId,
                materialDefinitionId = materialId,
                role = role,
                quantity = new MaterialQuantityData { value = value, unit = unit },
                purity = 1f
            };
        }

        private static MaterialDefinition Material(string id, MaterialCategory category, float density, float hardness, float durability)
        {
            MaterialDefinition material = UnityEngine.ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivate(material, "materialId", id);
            SetPrivate(material, "displayName", id);
            SetPrivate(material, "category", category);
            SetPrivate(material, "physicalProperties", new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = density,
                hardness = hardness,
                durability = durability,
                flexibility = 0.2f,
                conductivity = 0.2f,
                flammability = 0.1f,
                biologicalCompatibility = 0.5f
            });
            return material;
        }

        private static ProductionToolDefinition ProductionTool(string id, ProductionToolCategory category, ProductionToolRole[] roles, string[] capabilities, string[] substitutesFor = null, int priority = 0, float wear = 0f)
        {
            ProductionToolDefinition tool = UnityEngine.ScriptableObject.CreateInstance<ProductionToolDefinition>();
            SetPrivate(tool, "toolId", id);
            SetPrivate(tool, "displayName", id);
            SetPrivate(tool, "category", category);
            SetPrivate(tool, "roles", roles);
            SetPrivate(tool, "capabilityIds", capabilities);
            SetPrivate(tool, "substitutesForToolIds", substitutesFor ?? Array.Empty<string>());
            SetPrivate(tool, "minimumQuality", 0f);
            SetPrivate(tool, "minimumDurability", 0.01f);
            SetPrivate(tool, "durabilityWearPerUse", wear);
            SetPrivate(tool, "priority", priority);
            return tool;
        }

        private static ProductionStationDefinition ProductionStation(string id, ProductionStationCategory category, string[] capabilities, ProductionToolRole[] supportedRoles)
        {
            ProductionStationDefinition station = UnityEngine.ScriptableObject.CreateInstance<ProductionStationDefinition>();
            SetPrivate(station, "stationId", id);
            SetPrivate(station, "displayName", id);
            SetPrivate(station, "category", category);
            SetPrivate(station, "capabilityIds", capabilities);
            SetPrivate(station, "supportedToolRoles", supportedRoles);
            SetPrivate(station, "concurrentReservationLimit", 1);
            return station;
        }

        private static ProductionRequirementDefinition ProductionRequirement(
            string id,
            ProductionRequirementType type,
            ProductionToolDefinition tool = null,
            ProductionToolRole role = ProductionToolRole.Unknown,
            ProductionToolCategory category = ProductionToolCategory.Unknown,
            string capabilityId = "",
            ProductionStationDefinition station = null,
            ProductionStationCategory stationCategory = ProductionStationCategory.Unknown,
            string stationCapabilityId = "",
            string knowledgeId = "",
            string resourceId = "",
            ItemDefinition item = null,
            MaterialDefinition material = null,
            float quantity = 1f)
        {
            ProductionRequirementDefinition requirement = UnityEngine.ScriptableObject.CreateInstance<ProductionRequirementDefinition>();
            SetPrivate(requirement, "requirementId", id);
            SetPrivate(requirement, "displayName", id);
            SetPrivate(requirement, "requirementGroupId", "requirement-group.prototype.production");
            SetPrivate(requirement, "requirementType", type);
            SetPrivate(requirement, "strictness", ProductionRequirementStrictness.Required);
            SetPrivate(requirement, "allowSubstitution", true);
            SetPrivate(requirement, "toolDefinition", tool);
            SetPrivate(requirement, "toolRole", role);
            SetPrivate(requirement, "toolCategory", category);
            SetPrivate(requirement, "toolCapabilityId", capabilityId);
            SetPrivate(requirement, "stationDefinition", station);
            SetPrivate(requirement, "stationCategory", stationCategory);
            SetPrivate(requirement, "stationCapabilityId", stationCapabilityId);
            SetPrivate(requirement, "capabilityId", capabilityId);
            SetPrivate(requirement, "knowledgeFactDefinitionId", knowledgeId);
            SetPrivate(requirement, "resourceId", resourceId);
            SetPrivate(requirement, "itemDefinition", item);
            SetPrivate(requirement, "materialDefinition", material);
            SetPrivate(requirement, "quantity", quantity);
            return requirement;
        }

        private static ProductionToolCandidateData ToolCandidate(string itemInstanceId, ProductionToolDefinition tool, bool perceived = true, bool authoritative = true, float durability = 1f)
        {
            return new ProductionToolCandidateData
            {
                itemInstanceId = itemInstanceId,
                toolDefinitionId = tool.Id,
                role = tool.Roles.FirstOrDefault(),
                category = tool.Category,
                capabilityIds = tool.CapabilityIds.ToArray(),
                quality = 1f,
                durability = durability,
                perceived = perceived,
                authoritative = authoritative
            };
        }

        private static ProductionQuantityData ProductionQuantity(string id, float quantity, string itemInstanceId = "", string sourceContainerId = "", string locationId = "", long revision = 0L, long stackRevision = 0L)
        {
            return new ProductionQuantityData
            {
                definitionId = id,
                itemInstanceId = itemInstanceId,
                sourceContainerId = sourceContainerId,
                locationId = locationId,
                quantity = quantity,
                unit = ProductionQuantityUnit.Count,
                expectedRuntimeRevision = revision,
                expectedStackRevision = stackRevision,
                accessDecisionId = string.IsNullOrWhiteSpace(itemInstanceId) ? string.Empty : $"access.{itemInstanceId}",
                perceived = true,
                authoritative = true
            };
        }

        private static DefinitionRegistry ExtendRegistry(DefinitionRegistry registry, params IGameDefinition[] additions)
        {
            IEnumerable<IGameDefinition> existing = registry == null ? Array.Empty<IGameDefinition>() : registry.DefinitionsById.Values;
            IGameDefinition[] newDefinitions = additions ?? Array.Empty<IGameDefinition>();
            return new DefinitionRegistry(existing
                .Where(definition => definition != null && !newDefinitions.Any(addition => addition != null && string.Equals(addition.Id, definition.Id, StringComparison.Ordinal)))
                .Concat(newDefinitions));
        }

        private static CompositeMaterialConstituentDefinition Constituent(MaterialDefinition material, float ratio)
        {
            CompositeMaterialConstituentDefinition constituent = new CompositeMaterialConstituentDefinition();
            SetPrivate(constituent, "material", material);
            SetPrivate(constituent, "ratio", ratio);
            return constituent;
        }

        private static RecipeDefinition PrototypeRecipe(ItemDefinition sword, MaterialDefinition iron)
        {
            RecipeDefinition recipe = UnityEngine.ScriptableObject.CreateInstance<RecipeDefinition>();
            SetPrivate(recipe, "recipeId", "recipe.prototype.sword");
            SetPrivate(recipe, "displayName", "Prototype Sword Recipe");
            SetPrivate(recipe, "category", RecipeCategory.Smithing);
            SetPrivate(recipe, "currentVersionId", "recipe-version.prototype.sword.v1");
            SetPrivate(recipe, "versions", new[]
            {
                new RecipeVersionData { versionId = "recipe-version.prototype.sword.v0", versionLabel = "Old", state = RecipeLifecycleState.Deprecated },
                new RecipeVersionData { versionId = "recipe-version.prototype.sword.v1", versionLabel = "Current", priorVersionId = "recipe-version.prototype.sword.v0" }
            });
            SetPrivate(recipe, "variants", new[]
            {
                new RecipeVariantData
                {
                    variantId = "recipe-variant.prototype.decorated",
                    baseVersionId = "recipe-version.prototype.sword.v1",
                    additionalInputs = new[] { RecipeInput("recipe-input.prototype.trim", RecipeInputRole.DecorativeComponent, iron.Id, 0.25f, false, RecipeRequirementState.Optional) }
                }
            });
            SetPrivate(recipe, "inputs", new[]
            {
                RecipeInput("recipe-input.prototype.iron", RecipeInputRole.PrimaryMaterial, iron.Id, 2f, false, RecipeRequirementState.Required),
                RecipeInput("recipe-input.prototype.hidden-technique", RecipeInputRole.Catalyst, iron.Id, 0.1f, true, RecipeRequirementState.Required)
            });
            SetPrivate(recipe, "outputs", new[]
            {
                new RecipeOutputSpecificationData { outputId = "recipe-output.prototype.sword", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = sword.Id, quantity = 1f },
                new RecipeOutputSpecificationData { outputId = "recipe-output.prototype.scrap", role = RecipeOutputRole.Scrap, materialDefinitionId = iron.Id, quantity = 0.1f, conditional = true }
            });
            SetPrivate(recipe, "procedureSteps", new[]
            {
                RecipeStep("recipe-step.prototype.prepare", RecipeProcedureStepKind.PrepareInput),
                RecipeStep("recipe-step.prototype.shape", RecipeProcedureStepKind.Shape, "recipe-step.prototype.prepare"),
                RecipeStep("recipe-step.prototype.finish", RecipeProcedureStepKind.Finish, "recipe-step.prototype.shape")
            });
            SetPrivate(recipe, "transferMappings", new[]
            {
                new RecipeTransferMappingData { mappingId = "recipe-transfer.prototype.iron-to-sword", sourceInputId = "recipe-input.prototype.iron", targetOutputId = "recipe-output.prototype.sword", quantityTransferPolicy = RecipeTransferPolicy.InputDerived }
            });
            SetPrivate(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Discrete, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 5f, batchIncrement = 1f });
            SetPrivate(recipe, "compositionTransferPolicyId", "recipe-policy.prototype.composition");
            SetPrivate(recipe, "qualityGenerationPolicyId", "recipe-policy.prototype.quality");
            SetPrivate(recipe, "affixGenerationPolicyId", "recipe-policy.prototype.affix");
            SetPrivate(recipe, "durabilityInitializationPolicyId", "recipe-policy.prototype.durability");
            return recipe;
        }

        private static RecipeDefinition PrototypeRecipeWithMissingOutput(RecipeDefinition source, MaterialDefinition iron)
        {
            RecipeDefinition recipe = UnityEngine.ScriptableObject.CreateInstance<RecipeDefinition>();
            SetPrivate(recipe, "recipeId", "recipe.prototype.broken-output");
            SetPrivate(recipe, "displayName", "Broken Output Recipe");
            SetPrivate(recipe, "category", RecipeCategory.Smithing);
            SetPrivate(recipe, "currentVersionId", "recipe-version.prototype.broken-output.v1");
            SetPrivate(recipe, "versions", new[]
            {
                new RecipeVersionData { versionId = "recipe-version.prototype.broken-output.v1", versionLabel = "Current" }
            });
            SetPrivate(recipe, "inputs", new[]
            {
                RecipeInput("recipe-input.prototype.iron", RecipeInputRole.PrimaryMaterial, iron.Id, 1f, false, RecipeRequirementState.Required)
            });
            SetPrivate(recipe, "outputs", new[]
            {
                new RecipeOutputSpecificationData { outputId = "recipe-output.prototype.missing", role = RecipeOutputRole.PrimaryOutput, itemDefinitionId = "item.prototype.missing", quantity = 1f }
            });
            SetPrivate(recipe, "procedureSteps", new[]
            {
                RecipeStep("recipe-step.prototype.prepare", RecipeProcedureStepKind.PrepareInput)
            });
            SetPrivate(recipe, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Fixed, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 1f, batchIncrement = 1f });
            return recipe;
        }

        private static ProductionChainDefinition PrototypeProductionChain(string chainId, string versionId, string recipeId, bool cyclic = false)
        {
            ProductionChainDefinition chain = UnityEngine.ScriptableObject.CreateInstance<ProductionChainDefinition>();
            SetPrivate(chain, "chainId", chainId);
            SetPrivate(chain, "displayName", "Prototype Sword Production Chain");
            SetPrivate(chain, "category", "smithing");
            SetPrivate(chain, "currentVersionId", versionId);
            SetPrivate(chain, "state", ProductionChainLifecycleState.Active);
            SetPrivate(chain, "batchConsistencyPolicy", ProductionBatchConsistencyPolicy.IdenticalAuthoritativeState);
            SetPrivate(chain, "partialBatchPolicy", ProductionPartialBatchPolicy.AllOrNothing);
            SetPrivate(chain, "inputPolicy", ProductionInputConsumptionPolicy.ReservedAtStartConsumedAtCompletion);
            ProductionStageDefinitionData prepare = new ProductionStageDefinitionData
            {
                stageId = "production-stage.prototype.prepare",
                displayName = "Prepare Materials",
                category = ProductionStageCategory.Preparation,
                recipeDefinitionId = recipeId,
                recipeVersionId = "recipe-version.prototype.sword.v1",
                requiredWorkUnits = 2f,
                estimatedDuration = 2f,
                progressModel = ProductionProgressModel.TimeBased,
                priority = 10,
                dependencyStageIds = cyclic ? new[] { "production-stage.prototype.finish" } : Array.Empty<string>()
            };
            ProductionStageDefinitionData finish = new ProductionStageDefinitionData
            {
                stageId = "production-stage.prototype.finish",
                displayName = "Finish Sword",
                category = ProductionStageCategory.QualityControl,
                recipeDefinitionId = recipeId,
                recipeVersionId = "recipe-version.prototype.sword.v1",
                requiredWorkUnits = 1f,
                estimatedDuration = 1f,
                progressModel = ProductionProgressModel.TimeBased,
                priority = 20,
                dependencyStageIds = new[] { "production-stage.prototype.prepare" }
            };
            SetPrivate(chain, "versions", new[]
            {
                new ProductionChainVersionData
                {
                    versionId = versionId,
                    chainDefinitionId = chainId,
                    state = ProductionChainLifecycleState.Active,
                    stages = new[] { prepare, finish }
                }
            });
            return chain;
        }

        private static CraftingExecutionRequest CraftingRequest(TestLabAutomationContext context, RecipeDefinition recipe, MaterialDefinition iron, string slug)
        {
            return new CraftingExecutionRequest
            {
                operationId = context.ScenarioContext.ScopedId("crafting-operation", slug),
                recipeId = recipe.Id,
                actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                ownerPersonId = context.ScenarioContext.Runtimes.PersonId,
                custodianPersonId = context.ScenarioContext.Runtimes.PersonId,
                locationId = "location.prototype.smithy",
                worldTime = $"world-time.{context.RunId}.{slug}",
                deterministicSeed = $"seed.{context.RunId}.{slug}",
                productionContext = new ProductionContextData
                {
                    actorPersonId = context.ScenarioContext.Runtimes.PersonId,
                    locationId = "location.prototype.smithy",
                    worldTime = $"world-time.{context.RunId}.{slug}",
                    materialQuantities =
                    {
                        new ProductionQuantityData { definitionId = iron.Id, sourceContainerId = context.ScenarioContext.ScopedId("container", $"materials-{slug}"), quantity = 6f, sourceTotalQuantity = 6f, unit = ProductionQuantityUnit.Kilogram }
                    }
                }
            };
        }

        private static RecipeInputSpecificationData RecipeInput(string id, RecipeInputRole role, string materialId, float quantity, bool hidden, RecipeRequirementState state)
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

        private static RecipeProcedureStepData RecipeStep(string id, RecipeProcedureStepKind kind, params string[] dependencies)
        {
            return new RecipeProcedureStepData
            {
                stepId = id,
                stepKind = kind,
                displayName = id,
                dependsOnStepIds = dependencies ?? Array.Empty<string>()
            };
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static string RunGuid(TestLabAutomationContext context, string slug)
        {
            string seed = $"{context?.RunId}.{context?.CurrentSuiteId}.{context?.CurrentScenarioId}.{slug}";
            return DeterministicGuid(seed);
        }

        private static string DeterministicGuid(string seed)
        {
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("D");
        }

        private static ITestLabAutomationSuite Suite(string suiteId, string displayName, string feature, int order, System.Collections.Generic.IReadOnlyList<string> required, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(suiteId, displayName, feature, $"{displayName} runtime integration scenarios.", order, TestLabAutomationCategory.Standard, includeInRunAll: true, requiredServices: required, scenarios: scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 20 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 20,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Items,
                requiredHostFeatures: TestLabHostFeature.AutomatedExecution,
                requiredDefinitionIds: new[] { SwordId, PotionId });
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static System.Collections.Generic.IReadOnlyList<string> Required(params string[] services)
        {
            return services.ToArray();
        }

        private static TestLabAutomationStepResult Pass(TestLabAutomationContext context, string stepId, string diagnostics)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, stepId);
            return new TestLabAutomationStepResult(stepId, stepId, TestLabAutomationStatus.Passed, "OperationSucceeded", "Succeeded", "Succeeded", string.Empty, transactionId, diagnostics);
        }

        private static TestLabAutomationStepResult Fail(TestLabAutomationContext context, string stepId, string diagnostics)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, stepId);
            return new TestLabAutomationStepResult(stepId, stepId, TestLabAutomationStatus.Failed, "OperationSucceeded", "Succeeded", "Failed", string.Empty, transactionId, diagnostics);
        }

        private static void TryRegister(TestLabAutomationRegistry registry, ITestLabAutomationSuite suite)
        {
            registry.TryRegister(suite, out _);
        }
    }
}
#endif
