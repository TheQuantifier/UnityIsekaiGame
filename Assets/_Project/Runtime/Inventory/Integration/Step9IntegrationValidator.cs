using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Inventory.Integration
{
    public static class Step9IntegrationValidator
    {
        public static readonly IReadOnlyList<Step9IntegrationAuthorityEntry> AuthorityMap = new[]
        {
            new Step9IntegrationAuthorityEntry("item.identity", "ItemInstanceIdentityRuntime", "Inventory", "Equipment", "WorldItemPickup", "CraftingExecutionRuntime", "ProductionWorkflowRuntime"),
            new Step9IntegrationAuthorityEntry("item.composition", "ItemCompositionRuntime", "ItemDefinition", "CraftingExecutionRuntime", "ProductionWorkflowRuntime"),
            new Step9IntegrationAuthorityEntry("item.quality-affix", "ItemQualityAffixRuntime", "ItemCompositionRuntime", "Equipment", "CraftingExecutionRuntime"),
            new Step9IntegrationAuthorityEntry("item.durability", "ItemDurabilityRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "Equipment", "ProductionRequirementRuntime"),
            new Step9IntegrationAuthorityEntry("production.requirements", "ProductionRequirementRuntime", "RecipeRuntime", "CraftingExecutionRuntime", "ProductionWorkflowRuntime"),
            new Step9IntegrationAuthorityEntry("recipe.knowledge", "RecipeKnowledgeRuntime", "PersonKnowledgeRuntime", "InformationAccessRuntime"),
            new Step9IntegrationAuthorityEntry("crafting.execution", "CraftingExecutionRuntime", "RecipeRuntime", "ProductionRequirementRuntime"),
            new Step9IntegrationAuthorityEntry("production.workflow", "ProductionWorkflowRuntime", "ProductionRequirementRuntime", "CraftingExecutionRuntime"),
            new Step9IntegrationAuthorityEntry("experimentation.discovery", "ExperimentationRuntime", "CraftingExecutionRuntime", "ProductionWorkflowRuntime", "PersonKnowledgeRuntime")
        };

        public static readonly IReadOnlyList<Step9IntegrationDependencyEntry> PersistenceDependencies = new[]
        {
            new Step9IntegrationDependencyEntry("ItemInstanceIdentityRuntime"),
            new Step9IntegrationDependencyEntry("ItemCompositionRuntime", "ItemInstanceIdentityRuntime"),
            new Step9IntegrationDependencyEntry("ItemQualityAffixRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime"),
            new Step9IntegrationDependencyEntry("ItemDurabilityRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime"),
            new Step9IntegrationDependencyEntry("ProductionRequirementRuntime", "ItemInstanceIdentityRuntime", "ItemDurabilityRuntime"),
            new Step9IntegrationDependencyEntry("RecipeKnowledgeRuntime"),
            new Step9IntegrationDependencyEntry("CraftingExecutionRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime", "ItemDurabilityRuntime", "ProductionRequirementRuntime", "RecipeKnowledgeRuntime"),
            new Step9IntegrationDependencyEntry("ProductionWorkflowRuntime", "ItemInstanceIdentityRuntime", "ItemCompositionRuntime", "ItemQualityAffixRuntime", "ItemDurabilityRuntime", "ProductionRequirementRuntime", "CraftingExecutionRuntime"),
            new Step9IntegrationDependencyEntry("ExperimentationRuntime", "ItemInstanceIdentityRuntime", "ProductionRequirementRuntime", "CraftingExecutionRuntime", "ProductionWorkflowRuntime", "RecipeKnowledgeRuntime")
        };

        public static Step9IntegrationValidationReport ValidateDefinitions(DefinitionRegistry registry)
        {
            Step9IntegrationValidationReport report = new Step9IntegrationValidationReport();
            ValidateAuthorityMap(report);

            if (registry == null)
            {
                report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingRegistry", "Step 9 integration validation requires a definition registry.");
                return report;
            }

            string[] requiredDefinitionTypes =
            {
                nameof(ItemDefinition),
                nameof(MaterialDefinition),
                nameof(QualityTierDefinition),
                nameof(ItemAffixDefinition),
                nameof(ItemDegradationPolicyDefinition),
                nameof(RecipeDefinition),
                nameof(CraftingOutputPolicyDefinition),
                nameof(CraftingCatalystEffectDefinition),
                nameof(ProductionToolDefinition),
                nameof(ProductionStationDefinition),
                nameof(ProductionRequirementDefinition),
                nameof(ProductionChainDefinition),
                nameof(ExperimentDefinition)
            };

            HashSet<string> presentTypes = registry.DefinitionsById.Values
                .Where(definition => definition != null)
                .Select(definition => definition.GetType().Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string typeName in requiredDefinitionTypes)
            {
                if (!presentTypes.Contains(typeName))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingRepresentativeType", $"No catalog definition of type '{typeName}' is registered.", typeName);
                }
            }

            return report;
        }

        public static Step9IntegrationValidationReport ValidateRuntimeGraph(Step9IntegrationRuntimeSnapshot snapshot, DefinitionRegistry registry = null)
        {
            Step9IntegrationValidationReport report = ValidateDefinitions(registry);
            Step9IntegrationRuntimeSnapshot data = snapshot?.Clone() ?? new Step9IntegrationRuntimeSnapshot();
            ValidateSaveSchemas(data, report);

            Dictionary<string, ItemInstanceRecordData> items = ValidateItems(data.ItemInstances, registry, report);
            ValidateCompositions(data.ItemCompositions, registry, items, report);
            ValidateQualityAndAffixes(data.ItemQualityAffixes, registry, items, report);
            ValidateDurability(data.ItemDurability, registry, items, report);
            ValidateProductionRequirements(data.ProductionRequirements, items, report);
            ValidateRecipeKnowledge(data.RecipeKnowledge, registry, report);
            ValidateCrafting(data.CraftingExecution, registry, items, report);
            ValidateProductionWorkflow(data.ProductionWorkflow, items, report);
            ValidateExperimentation(data.Experimentation, registry, items, report);
            ValidatePersistenceDependencies(report);
            ValidateFingerprintDeterminism(data, report);

            return report;
        }

        public static string CreateCanonicalFingerprint(Step9IntegrationRuntimeSnapshot snapshot)
        {
            Step9IntegrationRuntimeSnapshot data = snapshot?.Clone() ?? new Step9IntegrationRuntimeSnapshot();
            StringBuilder builder = new StringBuilder(4096);

            AppendSection(builder, "items", data.ItemInstances.records, item => item.itemInstanceId, item =>
                $"{item.itemInstanceId}|{item.itemDefinitionId}|{item.classification}|{item.stackQuantity}|{item.lifecycleState}|{item.location?.kind}|{item.location?.containerId}|{item.location?.inventoryOwnerId}|{item.location?.equipmentHolderId}|{item.location?.equipmentSlotId}|{item.location?.worldPlacementId}|{item.location?.worldEntityId}|{item.location?.transitId}|{item.revision}");
            AppendSection(builder, "compositions", data.ItemCompositions.records, item => item.compositionId, item =>
                $"{item.compositionId}|{item.itemInstanceId}|{item.sourceItemDefinitionId}|{item.completeness}|{string.Join(",", Sorted(item.components?.Select(component => component.componentItemInstanceId)))}|{item.revision}");
            AppendSection(builder, "quality", data.ItemQualityAffixes.qualityRecords, item => item.qualityRecordId, item =>
                $"{item.qualityRecordId}|{item.itemInstanceId}|{item.itemDefinitionId}|{item.overallQuality:0.######}|{item.qualityTierId}|{item.revision}");
            AppendSection(builder, "affixes", data.ItemQualityAffixes.affixInstances, item => item.affixInstanceId, item =>
                $"{item.affixInstanceId}|{item.itemInstanceId}|{item.affixDefinitionId}|{item.affixTierId}|{item.active}|{item.removed}|{item.modifierSourceId}|{item.revision}");
            AppendSection(builder, "durability", data.ItemDurability.records, item => item.durabilityRecordId, item =>
                $"{item.durabilityRecordId}|{item.itemInstanceId}|{item.itemDefinitionId}|{item.currentDurability:0.######}|{item.maximumDurability:0.######}|{item.lastBreakCheckPercent}|{item.breakCheckSequence}|{item.hasBroken}|{item.pendingForcedDecomposition}|{item.functionalState}|{item.breakageState}|{item.revision}");
            AppendSection(builder, "production-plans", data.ProductionRequirements.plans, item => item.planId, item =>
                $"{item.planId}|{item.productionJobId}|{item.status}|{string.Join(",", Sorted(item.selections?.SelectMany(selection => selection.allocations ?? new List<ProductionInputAllocationData>()).Select(allocation => allocation.itemInstanceId)))}|{item.revision}");
            AppendSection(builder, "production-reservations", data.ProductionRequirements.reservations, item => item.reservationId, item =>
                $"{item.reservationId}|{item.planId}|{item.reservedItemInstanceId}|{item.reservedToolItemInstanceId}|{item.status}|{item.revision}");
            AppendSection(builder, "recipe-knowledge", data.RecipeKnowledge.records, item => item.recordId, item =>
                $"{item.recordId}|{item.personId}|{item.recipeId}|{item.versionId}|{item.variantId}|{item.completeness}|{item.incorrect}|{item.outdated}|{item.revision}");
            AppendSection(builder, "crafting", data.CraftingExecution.operations, item => item.operationId, item =>
                $"{item.operationId}|{item.recipeId}|{item.state}|{item.status}|{item.requirementPlanId}|{string.Join(",", Sorted(item.consumedInputs?.Select(input => input.itemInstanceId)))}|{string.Join(",", Sorted(item.catalysts?.Select(catalyst => $"{catalyst.slotId}:{catalyst.itemInstanceId}:{catalyst.quantity}:{catalyst.effectDefinitionId}:{catalyst.affixTierId}:{catalyst.effectApplied}")))}|{string.Join(",", Sorted(item.outputs?.Select(output => output.itemInstanceId)))}|{item.revision}");
            AppendSection(builder, "workflow-jobs", data.ProductionWorkflow.jobs, item => item.jobId, item =>
                $"{item.jobId}|{item.workOrderId}|{item.state}|{item.currentStageId}|{string.Join(",", Sorted(item.outputItemIds))}|{item.revision}");
            AppendSection(builder, "experiments", data.Experimentation.runs, item => item.experimentRunId, item =>
                $"{item.experimentRunId}|{item.experimentDefinitionId}|{item.planId}|{item.state}|{item.targetItemInstanceId}|{string.Join(",", Sorted(item.inputItemIds))}|{string.Join(",", Sorted(item.toolIds))}|{item.revision}");

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void ValidateAuthorityMap(Step9IntegrationValidationReport report)
        {
            string[] duplicateDomains = AuthorityMap
                .GroupBy(entry => entry.Domain, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            foreach (string domain in duplicateDomains)
            {
                report.AddError(Step9IntegrationDiagnosticDomain.Authority, "DuplicateAuthorityDomain", "Step 9 authority domains must have exactly one owner.", domain);
            }

            foreach (Step9IntegrationAuthorityEntry entry in AuthorityMap)
            {
                if (string.IsNullOrWhiteSpace(entry.Domain) || string.IsNullOrWhiteSpace(entry.Owner))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.Authority, "IncompleteAuthorityEntry", "Step 9 authority entries must declare a domain and owner.", entry.Domain);
                }
            }
        }

        private static void ValidateSaveSchemas(Step9IntegrationRuntimeSnapshot snapshot, Step9IntegrationValidationReport report)
        {
            CheckSchema(report, "ItemInstanceIdentityRuntime", snapshot.ItemInstances.schemaVersion, ItemInstanceRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ItemCompositionRuntime", snapshot.ItemCompositions.schemaVersion, ItemCompositionRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ItemQualityAffixRuntime", snapshot.ItemQualityAffixes.schemaVersion, ItemQualityAffixRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ItemDurabilityRuntime", snapshot.ItemDurability.schemaVersion, ItemDurabilityRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ProductionRequirementRuntime", snapshot.ProductionRequirements.schemaVersion, ProductionRequirementRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "RecipeKnowledgeRuntime", snapshot.RecipeKnowledge.schemaVersion, RecipeKnowledgeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "CraftingExecutionRuntime", snapshot.CraftingExecution.schemaVersion, CraftingExecutionRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ProductionWorkflowRuntime", snapshot.ProductionWorkflow.schemaVersion, ProductionWorkflowRuntimeSaveData.CurrentSchemaVersion);
            CheckSchema(report, "ExperimentationRuntime", snapshot.Experimentation.schemaVersion, ExperimentationRuntimeSaveData.CurrentSchemaVersion);
        }

        private static Dictionary<string, ItemInstanceRecordData> ValidateItems(ItemInstanceRuntimeSaveData save, DefinitionRegistry registry, Step9IntegrationValidationReport report)
        {
            Dictionary<string, ItemInstanceRecordData> items = new Dictionary<string, ItemInstanceRecordData>(StringComparer.Ordinal);
            HashSet<string> occupiedLocations = new HashSet<string>(StringComparer.Ordinal);

            foreach (ItemInstanceRecordData item in save.records ?? new List<ItemInstanceRecordData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemInstanceId))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "MissingItemInstanceId", "Item identity records must declare an item instance ID.");
                    continue;
                }

                if (!items.TryAdd(item.itemInstanceId, item))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.RuntimeIndex, "DuplicateItemInstanceId", "Item identity runtime contains duplicate item instance IDs.", item.itemInstanceId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.itemDefinitionId))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "MissingItemDefinition", "Item identity records must declare the authored item definition ID.", item.itemInstanceId);
                }
                else if (registry != null && !registry.TryGet<ItemDefinition>(item.itemDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingItemDefinition", $"Item definition '{item.itemDefinitionId}' is not registered.", item.itemInstanceId);
                }

                ValidateLocation(item, occupiedLocations, report);
                ValidateStack(item, report);
            }

            return items;
        }

        private static void ValidateLocation(ItemInstanceRecordData item, HashSet<string> occupiedLocations, Step9IntegrationValidationReport report)
        {
            ItemLocationStateData location = item.location ?? new ItemLocationStateData();
            bool terminal = item.lifecycleState == ItemLifecycleState.Destroyed
                || item.lifecycleState == ItemLifecycleState.Consumed
                || item.lifecycleState == ItemLifecycleState.Depleted
                || item.lifecycleState == ItemLifecycleState.Disassembled;

            if (terminal && location.kind is not (ItemLocationKind.Destroyed or ItemLocationKind.Consumed or ItemLocationKind.Unassigned))
            {
                report.AddError(Step9IntegrationDiagnosticDomain.Location, "TerminalItemHasActiveLocation", "Terminal item states cannot remain in active inventory, equipment, transit, reservation, or world locations.", item.itemInstanceId);
            }

            switch (location.kind)
            {
                case ItemLocationKind.Inventory:
                    RequireField(location.inventoryOwnerId, "inventoryOwnerId", item.itemInstanceId, report);
                    break;
                case ItemLocationKind.Equipped:
                    RequireField(location.equipmentHolderId, "equipmentHolderId", item.itemInstanceId, report);
                    RequireField(location.equipmentSlotId, "equipmentSlotId", item.itemInstanceId, report);
                    AddUniqueLocation(occupiedLocations, $"equipped:{location.equipmentHolderId}:{location.equipmentSlotId}", item.itemInstanceId, report);
                    break;
                case ItemLocationKind.WorldPlacement:
                    RequireField(location.worldPlacementId, "worldPlacementId", item.itemInstanceId, report);
                    AddUniqueLocation(occupiedLocations, $"world:{location.sceneKey}:{location.worldPlacementId}:{location.worldEntityId}", item.itemInstanceId, report);
                    break;
                case ItemLocationKind.Container:
                    RequireField(location.containerId, "containerId", item.itemInstanceId, report);
                    break;
                case ItemLocationKind.Transit:
                    RequireField(location.transitId, "transitId", item.itemInstanceId, report);
                    break;
            }
        }

        private static void ValidateStack(ItemInstanceRecordData item, Step9IntegrationValidationReport report)
        {
            if (item.stackQuantity < 1)
            {
                report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "InvalidStackQuantity", "Stack quantity must be at least one.", item.itemInstanceId);
            }

            if (item.classification is ItemInstanceClassification.Unique or ItemInstanceClassification.Serialized or ItemInstanceClassification.IndividuallyTracked
                && item.stackQuantity > 1)
            {
                report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "TrackedItemStacked", "Tracked, unique, and serialized item instances cannot carry stack quantities greater than one.", item.itemInstanceId);
            }
        }

        private static void ValidateCompositions(ItemCompositionRuntimeSaveData save, DefinitionRegistry registry, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            HashSet<string> compositionIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> composedItems = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> componentOwnerByItem = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (ItemCompositionRecordData composition in save.records ?? new List<ItemCompositionRecordData>())
            {
                if (composition == null || string.IsNullOrWhiteSpace(composition.compositionId))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "MissingCompositionId", "Composition records must declare a composition ID.");
                    continue;
                }

                if (!compositionIds.Add(composition.compositionId))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.RuntimeIndex, "DuplicateCompositionId", "Composition runtime contains duplicate composition IDs.", composition.compositionId);
                }

                if (!ReferenceItem(items, composition.itemInstanceId, composition.compositionId, "CompositionItemMissing", report))
                {
                    continue;
                }

                if (!composedItems.Add(composition.itemInstanceId))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "DuplicateCompositionForItem", "Only one authoritative composition record may exist for an item instance.", composition.itemInstanceId);
                }

                if (!string.IsNullOrWhiteSpace(composition.sourceItemDefinitionId) && registry != null && !registry.TryGet<ItemDefinition>(composition.sourceItemDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingCompositionSourceDefinition", $"Composition source item definition '{composition.sourceItemDefinitionId}' is not registered.", composition.compositionId);
                }

                foreach (ItemComponentEntryData component in composition.components ?? new List<ItemComponentEntryData>())
                {
                    if (!string.IsNullOrWhiteSpace(component.componentItemInstanceId))
                    {
                        ReferenceItem(items, component.componentItemInstanceId, composition.compositionId, "TrackedComponentMissing", report);
                        if (componentOwnerByItem.TryGetValue(component.componentItemInstanceId, out string owner) && owner != composition.compositionId)
                        {
                            report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "TrackedComponentMultiParent", "Tracked component item is attached to more than one composition.", component.componentItemInstanceId);
                        }
                        else
                        {
                            componentOwnerByItem[component.componentItemInstanceId] = composition.compositionId;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(component.componentItemDefinitionId) && registry != null && !registry.TryGet<ItemDefinition>(component.componentItemDefinitionId, out _))
                    {
                        report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingComponentDefinition", $"Component item definition '{component.componentItemDefinitionId}' is not registered.", composition.compositionId);
                    }
                }
            }
        }

        private static void ValidateQualityAndAffixes(ItemQualityAffixRuntimeSaveData save, DefinitionRegistry registry, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.qualityRecords, record => record.qualityRecordId, "DuplicateQualityRecordId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.affixInstances, affix => affix.affixInstanceId, "DuplicateAffixInstanceId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);

            foreach (ItemQualityRecordData record in save.qualityRecords ?? new List<ItemQualityRecordData>())
            {
                if (record == null)
                {
                    continue;
                }

                ReferenceItem(items, record.itemInstanceId, record.qualityRecordId, "QualityItemMissing", report);
                if (!string.IsNullOrWhiteSpace(record.itemDefinitionId) && registry != null && !registry.TryGet<ItemDefinition>(record.itemDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingQualityItemDefinition", $"Quality record item definition '{record.itemDefinitionId}' is not registered.", record.qualityRecordId);
                }
            }

            foreach (ItemAffixInstanceData affix in save.affixInstances ?? new List<ItemAffixInstanceData>())
            {
                if (affix == null)
                {
                    continue;
                }

                if (ReferenceItem(items, affix.itemInstanceId, affix.affixInstanceId, "AffixItemMissing", report)
                    && affix.active
                    && items.TryGetValue(affix.itemInstanceId, out ItemInstanceRecordData item)
                    && IsTerminal(item.lifecycleState))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "ActiveAffixOnTerminalItem", "Active affix modifiers cannot remain on terminal item instances.", affix.affixInstanceId);
                }

                if (!string.IsNullOrWhiteSpace(affix.affixDefinitionId) && registry != null && !registry.TryGet<ItemAffixDefinition>(affix.affixDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingAffixDefinition", $"Affix definition '{affix.affixDefinitionId}' is not registered.", affix.affixInstanceId);
                }
            }
        }

        private static void ValidateDurability(ItemDurabilityRuntimeSaveData save, DefinitionRegistry registry, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.records, record => record.durabilityRecordId, "DuplicateDurabilityRecordId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);

            foreach (ItemDurabilityRecordData record in save.records ?? new List<ItemDurabilityRecordData>())
            {
                if (record == null)
                {
                    continue;
                }

                if (ReferenceItem(items, record.itemInstanceId, record.durabilityRecordId, "DurabilityItemMissing", report)
                    && record.breakageState == ItemBreakageState.Destroyed
                    && items.TryGetValue(record.itemInstanceId, out ItemInstanceRecordData item)
                    && !IsTerminal(item.lifecycleState))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "DestroyedDurabilityOnActiveItem", "Destroyed durability state must be reflected by terminal item identity state.", record.durabilityRecordId);
                }

                if (record.maximumDurability < 0f || record.currentDurability < 0f || record.currentDurability > record.maximumDurability)
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "InvalidDurabilityRange", "Durability values must stay within 0..maximum durability.", record.durabilityRecordId);
                }

                if (record.lastBreakCheckPercent < 1
                    || record.lastBreakCheckPercent > 11
                    || record.breakCheckSequence < 0L)
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "InvalidBreakCheckState", "Item break-check chance, percentage cursor, and sequence must be valid.", record.durabilityRecordId);
                }

                if (!string.IsNullOrWhiteSpace(record.itemDefinitionId) && registry != null && !registry.TryGet<ItemDefinition>(record.itemDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingDurabilityItemDefinition", $"Durability item definition '{record.itemDefinitionId}' is not registered.", record.durabilityRecordId);
                }
            }
        }

        private static void ValidateProductionRequirements(ProductionRequirementRuntimeSaveData save, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.stations, station => station.stationInstanceId, "DuplicateProductionStationId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.plans, plan => plan.planId, "DuplicateProductionPlanId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.reservations, reservation => reservation.reservationId, "DuplicateProductionReservationId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);

            HashSet<string> activeReservationsByItem = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionReservationData reservation in save.reservations ?? new List<ProductionReservationData>())
            {
                if (reservation == null || reservation.status != ProductionReservationStatus.Active)
                {
                    continue;
                }

                CheckReservationItem(items, activeReservationsByItem, reservation.reservedItemInstanceId, reservation.reservationId, report);
                CheckReservationItem(items, activeReservationsByItem, reservation.reservedToolItemInstanceId, reservation.reservationId, report);
            }

            foreach (ProductionRequirementPlanData plan in save.plans ?? new List<ProductionRequirementPlanData>())
            {
                foreach (ProductionInputAllocationData allocation in plan?.selections?.SelectMany(selection => selection.allocations ?? new List<ProductionInputAllocationData>()) ?? Enumerable.Empty<ProductionInputAllocationData>())
                {
                    ReferenceItem(items, allocation.itemInstanceId, plan.planId, "ProductionAllocationItemMissing", report, allowEmpty: true);
                }
            }
        }

        private static void ValidateRecipeKnowledge(RecipeKnowledgeSaveData save, DefinitionRegistry registry, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.records, record => record.recordId, "DuplicateRecipeKnowledgeRecordId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            foreach (RecipeKnowledgeRecordData record in save.records ?? new List<RecipeKnowledgeRecordData>())
            {
                if (record != null && registry != null && !string.IsNullOrWhiteSpace(record.recipeId) && !registry.TryGet<RecipeDefinition>(record.recipeId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingKnownRecipeDefinition", $"Known recipe '{record.recipeId}' is not registered.", record.recordId);
                }
            }
        }

        private static void ValidateCrafting(CraftingExecutionRuntimeSaveData save, DefinitionRegistry registry, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.operations, operation => operation.operationId, "DuplicateCraftingOperationId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            foreach (CraftingOperationRecordData operation in save.operations ?? new List<CraftingOperationRecordData>())
            {
                if (operation == null)
                {
                    continue;
                }

                if (registry != null && !string.IsNullOrWhiteSpace(operation.recipeId) && !registry.TryGet<RecipeDefinition>(operation.recipeId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingCraftingRecipe", $"Crafting operation recipe '{operation.recipeId}' is not registered.", operation.operationId);
                }

                foreach (CraftingConsumedInputData input in operation.consumedInputs ?? new List<CraftingConsumedInputData>())
                {
                    ReferenceItem(items, input.itemInstanceId, operation.operationId, "CraftingInputItemMissing", report, allowEmpty: true);
                }

                foreach (CraftingToolUseData tool in operation.toolUses ?? new List<CraftingToolUseData>())
                {
                    ReferenceItem(items, tool.toolItemInstanceId, operation.operationId, "CraftingToolItemMissing", report, allowEmpty: true);
                }

                HashSet<string> catalystSlots = new HashSet<string>(StringComparer.Ordinal);
                foreach (CraftingCatalystUseData catalyst in operation.catalysts ?? new List<CraftingCatalystUseData>())
                {
                    if (catalyst == null || string.IsNullOrWhiteSpace(catalyst.slotId) || !catalystSlots.Add(catalyst.slotId))
                    {
                        report.AddError(Step9IntegrationDiagnosticDomain.RuntimeIndex, "InvalidCraftingCatalystSlot", "Crafting catalyst slots must be non-empty and unique.", operation.operationId);
                        continue;
                    }

                    ReferenceItem(items, catalyst.itemInstanceId, operation.operationId, "CraftingCatalystItemMissing", report);
                    if (registry != null && !registry.TryGet<ItemDefinition>(catalyst.itemDefinitionId, out _))
                    {
                        report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingCraftingCatalystItemDefinition", $"Crafting catalyst item '{catalyst.itemDefinitionId}' is not registered.", operation.operationId);
                    }

                    if (registry != null
                        && !string.IsNullOrWhiteSpace(catalyst.effectDefinitionId)
                        && !registry.TryGet<CraftingCatalystEffectDefinition>(catalyst.effectDefinitionId, out _))
                    {
                        report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingCraftingCatalystEffect", $"Crafting catalyst effect '{catalyst.effectDefinitionId}' is not registered.", operation.operationId);
                    }
                }

                foreach (CraftingOutputItemData output in operation.outputs ?? new List<CraftingOutputItemData>())
                {
                    if (!string.IsNullOrWhiteSpace(output.itemInstanceId) && !output.createdItemInstance)
                    {
                        ReferenceItem(items, output.itemInstanceId, operation.operationId, "CraftingOutputItemMissing", report);
                    }
                }
            }
        }

        private static void ValidateProductionWorkflow(ProductionWorkflowRuntimeSaveData save, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.workOrders, item => item.workOrderId, "DuplicateWorkOrderId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.jobs, item => item.jobId, "DuplicateProductionJobId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.queues, item => item.queueId, "DuplicateProductionQueueId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.batches, item => item.batchId, "DuplicateProductionBatchId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.lots, item => item.lotId, "DuplicateProductionLotId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);

            foreach (ProductionWorkOrderData order in save.workOrders ?? new List<ProductionWorkOrderData>())
            {
                foreach (string outputItemId in order?.outputItemIds ?? Array.Empty<string>())
                {
                    ReferenceItem(items, outputItemId, order.workOrderId, "WorkOrderOutputItemMissing", report);
                }
            }

            foreach (ProductionJobData job in save.jobs ?? new List<ProductionJobData>())
            {
                foreach (string outputItemId in job?.outputItemIds ?? Array.Empty<string>())
                {
                    ReferenceItem(items, outputItemId, job.jobId, "ProductionJobOutputItemMissing", report);
                }
            }
        }

        private static void ValidateExperimentation(ExperimentationRuntimeSaveData save, DefinitionRegistry registry, Dictionary<string, ItemInstanceRecordData> items, Step9IntegrationValidationReport report)
        {
            ValidateUnique(save.hypotheses, item => item.hypothesisId, "DuplicateHypothesisId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.plans, item => item.planId, "DuplicateExperimentPlanId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);
            ValidateUnique(save.runs, item => item.experimentRunId, "DuplicateExperimentRunId", Step9IntegrationDiagnosticDomain.RuntimeIndex, report);

            foreach (ExperimentPlanData plan in save.plans ?? new List<ExperimentPlanData>())
            {
                if (plan == null)
                {
                    continue;
                }

                if (registry != null && !string.IsNullOrWhiteSpace(plan.recipeDefinitionId) && !registry.TryGet<RecipeDefinition>(plan.recipeDefinitionId, out _))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.DefinitionCatalog, "MissingExperimentRecipe", $"Experiment plan recipe '{plan.recipeDefinitionId}' is not registered.", plan.planId);
                }

                foreach (string itemId in (plan.inputItemInstanceIds ?? Array.Empty<string>()).Concat(plan.toolItemInstanceIds ?? Array.Empty<string>()))
                {
                    ReferenceItem(items, itemId, plan.planId, "ExperimentPlanItemMissing", report);
                }
            }

            foreach (ExperimentRunData run in save.runs ?? new List<ExperimentRunData>())
            {
                if (run == null)
                {
                    continue;
                }

                ReferenceItem(items, run.targetItemInstanceId, run.experimentRunId, "ExperimentTargetItemMissing", report, allowEmpty: true);
                foreach (string itemId in (run.inputItemIds ?? Array.Empty<string>()).Concat(run.toolIds ?? Array.Empty<string>()))
                {
                    ReferenceItem(items, itemId, run.experimentRunId, "ExperimentRunItemMissing", report);
                }
            }
        }

        private static void ValidatePersistenceDependencies(Step9IntegrationValidationReport report)
        {
            HashSet<string> owners = PersistenceDependencies.Select(entry => entry.Owner).ToHashSet(StringComparer.Ordinal);
            foreach (Step9IntegrationDependencyEntry entry in PersistenceDependencies)
            {
                foreach (string dependency in entry.DependsOn)
                {
                    if (!owners.Contains(dependency))
                    {
                        report.AddError(Step9IntegrationDiagnosticDomain.Persistence, "MissingPersistenceDependencyOwner", $"Persistence dependency '{dependency}' is not declared as a Step 9 owner.", entry.Owner);
                    }
                }
            }

            foreach (Step9IntegrationDependencyEntry entry in PersistenceDependencies)
            {
                if (HasCycle(entry.Owner, entry.Owner, new HashSet<string>(StringComparer.Ordinal)))
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.Persistence, "CyclicPersistenceDependency", "Step 9 persistence dependencies must remain acyclic.", entry.Owner);
                }
            }
        }

        private static void ValidateFingerprintDeterminism(Step9IntegrationRuntimeSnapshot snapshot, Step9IntegrationValidationReport report)
        {
            string first = CreateCanonicalFingerprint(snapshot);
            string second = CreateCanonicalFingerprint(snapshot.Clone());
            if (!string.Equals(first, second, StringComparison.Ordinal))
            {
                report.AddError(Step9IntegrationDiagnosticDomain.Determinism, "FingerprintNondeterministic", "Step 9 canonical runtime fingerprint changed for equivalent snapshots.");
            }
        }

        private static bool HasCycle(string root, string current, HashSet<string> visited)
        {
            if (!visited.Add(current))
            {
                return false;
            }

            Step9IntegrationDependencyEntry entry = PersistenceDependencies.FirstOrDefault(candidate => string.Equals(candidate.Owner, current, StringComparison.Ordinal));
            if (entry == null)
            {
                return false;
            }

            foreach (string dependency in entry.DependsOn)
            {
                if (string.Equals(dependency, root, StringComparison.Ordinal) || HasCycle(root, dependency, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReferenceItem(Dictionary<string, ItemInstanceRecordData> items, string itemId, string ownerId, string code, Step9IntegrationValidationReport report, bool allowEmpty = false)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                if (!allowEmpty)
                {
                    report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, code, "Cross-runtime item reference is empty.", ownerId);
                }

                return false;
            }

            if (items.ContainsKey(itemId))
            {
                return true;
            }

            report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, code, $"Referenced item instance '{itemId}' is not owned by ItemInstanceIdentityRuntime.", ownerId);
            return false;
        }

        private static void CheckReservationItem(Dictionary<string, ItemInstanceRecordData> items, HashSet<string> activeReservationsByItem, string itemId, string reservationId, Step9IntegrationValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            ReferenceItem(items, itemId, reservationId, "ProductionReservationItemMissing", report);
            if (!activeReservationsByItem.Add(itemId))
            {
                report.AddError(Step9IntegrationDiagnosticDomain.ItemGraph, "ItemReservedByMultipleActiveReservations", "An item instance cannot be held by multiple active production reservations.", itemId);
            }
        }

        private static void RequireField(string value, string fieldName, string itemId, Step9IntegrationValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddError(Step9IntegrationDiagnosticDomain.Location, "MissingLocationField", $"Location field '{fieldName}' is required for this location kind.", itemId);
            }
        }

        private static void AddUniqueLocation(HashSet<string> occupiedLocations, string key, string itemId, Step9IntegrationValidationReport report)
        {
            if (!occupiedLocations.Add(key))
            {
                report.AddError(Step9IntegrationDiagnosticDomain.Location, "DuplicateExclusiveLocation", "Two item instances occupy the same exclusive location.", itemId);
            }
        }

        private static void CheckSchema(Step9IntegrationValidationReport report, string owner, int actual, int expected)
        {
            if (actual != expected)
            {
                report.AddError(Step9IntegrationDiagnosticDomain.SaveSchema, "UnsupportedSchemaVersion", $"'{owner}' save schema version {actual} is not supported; expected {expected}.", owner);
            }
        }

        private static bool IsTerminal(ItemLifecycleState state)
        {
            return state is ItemLifecycleState.Destroyed or ItemLifecycleState.Consumed or ItemLifecycleState.Depleted or ItemLifecycleState.Disassembled;
        }

        private static void ValidateUnique<T>(IEnumerable<T> values, Func<T, string> idSelector, string code, Step9IntegrationDiagnosticDomain domain, Step9IntegrationValidationReport report)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                if (value == null)
                {
                    continue;
                }

                string id = idSelector(value) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.AddError(domain, code.Replace("Duplicate", "Missing"), "Runtime records must declare stable IDs.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    report.AddError(domain, code, "Runtime index contains duplicate stable IDs.", id);
                }
            }
        }

        private static void AppendSection<T>(StringBuilder builder, string name, IEnumerable<T> values, Func<T, string> idSelector, Func<T, string> serializer)
        {
            builder.Append(name).Append(':');
            foreach (T value in (values ?? Array.Empty<T>()).Where(value => value != null).OrderBy(value => idSelector(value) ?? string.Empty, StringComparer.Ordinal))
            {
                builder.Append(serializer(value)).Append(';');
            }

            builder.AppendLine();
        }

        private static IEnumerable<string> Sorted(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
        }
    }
}
