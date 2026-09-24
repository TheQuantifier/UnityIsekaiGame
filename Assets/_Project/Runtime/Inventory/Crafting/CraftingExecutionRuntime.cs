using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Inventory.Crafting
{
    public sealed class CraftingExecutionRuntime
    {
        private readonly Dictionary<string, CraftingOperationRecordData> operationsById = new Dictionary<string, CraftingOperationRecordData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int OperationCount => operationsById.Count;
        public IReadOnlyList<CraftingOperationRecordData> Operations => operationsById.Values.OrderBy(entry => entry.operationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();

        public bool TryGetOperation(string operationId, out CraftingOperationRecordData operation)
        {
            if (!string.IsNullOrWhiteSpace(operationId) && operationsById.TryGetValue(operationId, out CraftingOperationRecordData found))
            {
                operation = found.Clone();
                return true;
            }

            operation = null;
            return false;
        }

        public CraftingExecutionResult Preview(
            CraftingExecutionRequest request,
            DefinitionRegistry registry,
            RecipeRuntime recipeRuntime,
            ProductionRequirementRuntime productionRuntime,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemDurabilityRuntime durabilityRuntime)
        {
            CraftingExecutionRequest preview = request?.Clone() ?? new CraftingExecutionRequest();
            preview.preview = true;
            return Execute(preview, registry, recipeRuntime, productionRuntime, itemRuntime, null, null, durabilityRuntime);
        }

        public CraftingExecutionResult Execute(
            CraftingExecutionRequest request,
            DefinitionRegistry registry,
            RecipeRuntime recipeRuntime,
            ProductionRequirementRuntime productionRuntime,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime)
        {
            CraftingExecutionRequest working = request?.Clone() ?? new CraftingExecutionRequest();
            NormalizeRequest(working);
            if (!ValidateExecutionInputs(working, registry, recipeRuntime, productionRuntime, itemRuntime, out string validationFailure))
            {
                return CraftingExecutionResult.Failure(CraftingExecutionStatus.InvalidRequest, validationFailure);
            }

            if (operationsById.TryGetValue(working.operationId, out CraftingOperationRecordData existing))
            {
                if (existing.state == CraftingOperationState.Completed)
                {
                    return CraftingExecutionResult.Success(existing, "Crafting operation was already completed.", duplicate: true);
                }

                return CraftingExecutionResult.Failure(CraftingExecutionStatus.InvalidRequest, $"Crafting operation '{working.operationId}' already exists in state {existing.state}.", existing);
            }

            RuntimeRollbackSnapshot rollback = working.preview
                ? null
                : RuntimeRollbackSnapshot.Capture(itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime, this);
            RecipeResolutionRequest recipeRequest = BuildRecipeRequest(working);
            RecipeResolutionResult recipeResult = recipeRuntime.Resolve(recipeRequest, registry, productionRuntime, itemRuntime, durabilityRuntime);
            CraftingOperationRecordData operation = CreateOperation(working, recipeResult);
            if (recipeResult == null || !recipeResult.Succeeded)
            {
                operation.state = CraftingOperationState.Failed;
                operation.status = recipeResult == null || recipeResult.Status == RecipeResolutionStatus.MissingRecipe ? CraftingExecutionStatus.MissingRecipe : CraftingExecutionStatus.RequirementFailed;
                operation.diagnostics = AddDiagnostics(operation.diagnostics, recipeResult?.Message ?? "Recipe resolution failed.");
                if (rollback != null && !rollback.Restore(registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime, this, out string rollbackFailure))
                {
                    operation.status = CraftingExecutionStatus.RollbackFailed;
                    operation.diagnostics = AddDiagnostics(operation.diagnostics, rollbackFailure);
                    return CraftingExecutionResult.Failure(CraftingExecutionStatus.RollbackFailed, rollbackFailure, operation, recipeResult);
                }

                return CraftingExecutionResult.Failure(operation.status, recipeResult?.Message ?? "Recipe resolution failed.", operation, recipeResult);
            }

            if (!ValidateCatalystAvailability(operation, recipeResult.RequirementResult?.Plan, itemRuntime, out string catalystValidationFailure))
            {
                return FailAndRollback(CraftingExecutionStatus.RequirementFailed, catalystValidationFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
            }

            if (working.preview)
            {
                operation.state = CraftingOperationState.Prepared;
                operation.status = CraftingExecutionStatus.Preview;
                return CraftingExecutionResult.Success(operation, "Crafting execution preview prepared.", preview: true, recipeResult: recipeResult);
            }

            operation.state = CraftingOperationState.Executing;
            operation.status = CraftingExecutionStatus.Succeeded;

            try
            {
                ProductionRequirementEvaluationResult current = productionRuntime.ValidatePlanCurrent(recipeResult.RequirementResult?.Plan?.planId, itemRuntime, durabilityRuntime);
                if (current == null || !current.Succeeded)
                {
                    return FailAndRollback(CraftingExecutionStatus.StalePlan, current?.Message ?? "Crafting requirement plan is stale.", operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                if (!ConsumeInputs(operation, recipeResult.RequirementResult?.Plan, recipeResult.Snapshot, itemRuntime, out string consumeFailure))
                {
                    return FailAndRollback(CraftingExecutionStatus.InputConsumptionFailed, consumeFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                if (!ConsumeCatalysts(operation, itemRuntime, out string catalystConsumeFailure))
                {
                    return FailAndRollback(CraftingExecutionStatus.InputConsumptionFailed, catalystConsumeFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                if (!CreateOutputs(operation, recipeResult.Snapshot, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, out string outputFailure))
                {
                    return FailAndRollback(CraftingExecutionStatus.OutputCreationFailed, outputFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                if (!ApplyCatalystEffects(operation, registry, itemRuntime, compositionRuntime, qualityRuntime, out string catalystEffectFailure))
                {
                    return FailAndRollback(CraftingExecutionStatus.OutputCreationFailed, catalystEffectFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                if (!ApplyToolWear(operation, recipeResult.RequirementResult?.Plan, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, registry, out string toolFailure))
                {
                    return FailAndRollback(CraftingExecutionStatus.ToolWearFailed, toolFailure, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                ProductionReservationResult release = productionRuntime.ReleasePlanReservations(recipeResult.RequirementResult?.Plan?.planId);
                if (release == null || !release.Succeeded)
                {
                    return FailAndRollback(CraftingExecutionStatus.ReservationFailed, release?.Message ?? "Crafting reservation release failed.", operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
                }

                operation.state = CraftingOperationState.Completed;
                operation.status = CraftingExecutionStatus.Succeeded;
                operation.revision = 1L;
                operationsById.Add(operation.operationId, operation.Clone());
                revision++;
                return CraftingExecutionResult.Success(operation, "Crafting execution completed.", recipeResult: recipeResult);
            }
            catch (Exception ex)
            {
                return FailAndRollback(CraftingExecutionStatus.ValidationFailed, ex.Message, operation, recipeResult, rollback, registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime);
            }
        }

        public CraftingExecutionRuntimeSaveData CreateSaveData()
        {
            return new CraftingExecutionRuntimeSaveData
            {
                schemaVersion = CraftingExecutionRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                operations = operationsById.Values.OrderBy(entry => entry.operationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList()
            };
        }

        public CraftingExecutionResult RestoreFromSaveData(CraftingExecutionRuntimeSaveData saveData, DefinitionRegistry registry)
        {
            if (!ValidateSaveData(saveData, registry, out string failure))
            {
                return CraftingExecutionResult.Failure(CraftingExecutionStatus.RestoreFailed, failure);
            }

            operationsById.Clear();
            foreach (CraftingOperationRecordData operation in saveData.operations.Select(entry => entry.Clone()).OrderBy(entry => entry.operationId, StringComparer.Ordinal))
            {
                operationsById[operation.operationId] = operation;
            }

            revision = Math.Max(0L, saveData.revision);
            return CraftingExecutionResult.Success(null, "Crafting execution runtime restored.");
        }

        public static bool ValidateSaveData(CraftingExecutionRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Crafting execution save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != CraftingExecutionRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported crafting execution schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failure = "Crafting execution revision cannot be negative.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CraftingOperationRecordData operation in saveData.operations ?? new List<CraftingOperationRecordData>())
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.operationId))
                {
                    failure = "Crafting operation is missing an operation ID.";
                    return false;
                }

                if (!ids.Add(operation.operationId))
                {
                    failure = $"Duplicate crafting operation ID '{operation.operationId}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(operation.recipeId))
                {
                    failure = $"Crafting operation '{operation.operationId}' is missing a recipe ID.";
                    return false;
                }

                if (registry != null && !registry.TryGet(operation.recipeId, out RecipeDefinition _))
                {
                    failure = $"Crafting operation '{operation.operationId}' references missing recipe '{operation.recipeId}'.";
                    return false;
                }

                if (operation.craftDurationSeconds < 0f
                    || float.IsNaN(operation.craftDurationSeconds)
                    || float.IsInfinity(operation.craftDurationSeconds)
                    || float.IsNaN(operation.craftingQualityAdjustment)
                    || float.IsInfinity(operation.craftingQualityAdjustment)
                    || operation.craftingAffixChanceBonus < 0f
                    || float.IsNaN(operation.craftingAffixChanceBonus)
                    || float.IsInfinity(operation.craftingAffixChanceBonus))
                {
                    failure = $"Crafting operation '{operation.operationId}' has invalid crafting scaling values.";
                    return false;
                }

                if (operation.craftingSkillUsed && (string.IsNullOrWhiteSpace(operation.craftingSkillId) || operation.craftingSkillGrade < 0 || operation.craftingSkillGrade > 7))
                {
                    failure = $"Crafting operation '{operation.operationId}' has invalid crafting Skill provenance.";
                    return false;
                }

                HashSet<string> catalystSlotIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (CraftingCatalystUseData catalyst in operation.catalysts ?? new List<CraftingCatalystUseData>())
                {
                    if (catalyst == null
                        || string.IsNullOrWhiteSpace(catalyst.slotId)
                        || !catalystSlotIds.Add(catalyst.slotId)
                        || string.IsNullOrWhiteSpace(catalyst.itemDefinitionId)
                        || string.IsNullOrWhiteSpace(catalyst.itemInstanceId)
                        || catalyst.quantity <= 0
                        || catalyst.effectChance < 0f
                        || catalyst.effectChance > 1f
                        || catalyst.effectRoll < 0f
                        || catalyst.effectRoll > 1f)
                    {
                        failure = $"Crafting operation '{operation.operationId}' has invalid catalyst data.";
                        return false;
                    }

                    if (registry != null && !registry.TryGet(catalyst.itemDefinitionId, out ItemDefinition _))
                    {
                        failure = $"Crafting operation '{operation.operationId}' references missing catalyst item '{catalyst.itemDefinitionId}'.";
                        return false;
                    }

                    if (registry != null
                        && !string.IsNullOrWhiteSpace(catalyst.effectDefinitionId)
                        && !registry.TryGet(catalyst.effectDefinitionId, out CraftingCatalystEffectDefinition _))
                    {
                        failure = $"Crafting operation '{operation.operationId}' references missing catalyst effect '{catalyst.effectDefinitionId}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateExecutionInputs(CraftingExecutionRequest request, DefinitionRegistry registry, RecipeRuntime recipeRuntime, ProductionRequirementRuntime productionRuntime, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            if (request == null)
            {
                failure = "Crafting execution request is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.operationId))
            {
                failure = "Crafting execution requires an operation ID.";
                return false;
            }

            if (registry == null || recipeRuntime == null || productionRuntime == null || itemRuntime == null)
            {
                failure = "Crafting execution requires recipe, production, item, and definition runtimes.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.recipeId))
            {
                failure = "Crafting execution requires a recipe ID.";
                return false;
            }

            return true;
        }

        private static RecipeResolutionRequest BuildRecipeRequest(CraftingExecutionRequest request)
        {
            ProductionContextData context = request.productionContext?.Clone() ?? new ProductionContextData();
            if (string.IsNullOrWhiteSpace(context.actorPersonId))
            {
                context.actorPersonId = request.actorPersonId;
            }

            if (string.IsNullOrWhiteSpace(context.actorBodyId))
            {
                context.actorBodyId = request.actorBodyId;
            }

            if (string.IsNullOrWhiteSpace(context.locationId))
            {
                context.locationId = request.locationId;
            }

            if (string.IsNullOrWhiteSpace(context.worldTime))
            {
                context.worldTime = request.worldTime;
            }

            return new RecipeResolutionRequest
            {
                recipeId = request.recipeId,
                versionId = request.versionId,
                variantId = request.variantId,
                batchSize = request.batchSize,
                selectedOptionalInputIds = request.selectedOptionalInputIds,
                accessLevel = request.accessLevel,
                productionContext = context,
                buildRequirementPlan = true,
                reservePlan = !request.preview,
                productionJobId = request.operationId,
                planId = $"crafting-plan.{request.operationId}",
                reservationExpiresWorldTime = request.worldTime
            };
        }

        private static CraftingOperationRecordData CreateOperation(CraftingExecutionRequest request, RecipeResolutionResult recipeResult)
        {
            RecipeResolvedSnapshot snapshot = recipeResult?.Snapshot;
            return new CraftingOperationRecordData
            {
                operationId = request.operationId,
                recipeId = request.recipeId,
                versionId = snapshot?.VersionId ?? request.versionId,
                variantId = snapshot?.VariantId ?? request.variantId,
                actorPersonId = request.actorPersonId,
                actorBodyId = request.actorBodyId,
                ownerPersonId = string.IsNullOrWhiteSpace(request.ownerPersonId) ? request.actorPersonId : request.ownerPersonId,
                locationId = request.locationId,
                worldTime = request.worldTime,
                deterministicSeed = request.deterministicSeed,
                craftingSkillId = request.craftingSkillId,
                craftingSkillGrade = request.craftingSkillGrade,
                craftingSkillUsed = request.craftingSkillUsed,
                craftDurationSeconds = request.craftDurationSeconds,
                craftingQualityAdjustment = request.craftingQualityAdjustment,
                craftingAffixChanceBonus = request.craftingAffixChanceBonus,
                catalysts = request.catalysts == null
                    ? new List<CraftingCatalystUseData>()
                    : request.catalysts.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                recipeSignature = snapshot?.Signature ?? string.Empty,
                requirementPlanId = recipeResult?.RequirementResult?.Plan?.planId ?? string.Empty,
                state = request.preview ? CraftingOperationState.Prepared : CraftingOperationState.Reserved,
                status = request.preview ? CraftingExecutionStatus.Preview : CraftingExecutionStatus.Succeeded,
                failurePolicy = request.failurePolicy
            };
        }

        private static bool ValidateCatalystAvailability(
            CraftingOperationRecordData operation,
            ProductionRequirementPlanData plan,
            ItemInstanceIdentityRuntime itemRuntime,
            out string failure)
        {
            failure = string.Empty;
            HashSet<string> slotIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> requiredByInstance = Allocations(plan)
                .Where(allocation => !allocation.reusable && !string.IsNullOrWhiteSpace(allocation.itemInstanceId))
                .GroupBy(allocation => allocation.itemInstanceId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(allocation => Math.Max(1, (int)Math.Round(allocation.quantity))), StringComparer.Ordinal);

            foreach (CraftingCatalystUseData catalyst in operation.catalysts ?? new List<CraftingCatalystUseData>())
            {
                if (string.IsNullOrWhiteSpace(catalyst.slotId) || !slotIds.Add(catalyst.slotId))
                {
                    failure = "Crafting catalyst slots require unique, non-empty slot IDs.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(catalyst.itemInstanceId)
                    || string.IsNullOrWhiteSpace(catalyst.itemDefinitionId)
                    || catalyst.quantity <= 0
                    || !itemRuntime.TryGetSnapshot(catalyst.itemInstanceId, out ItemInstanceSnapshot snapshot)
                    || !string.Equals(snapshot.ItemDefinitionId, catalyst.itemDefinitionId, StringComparison.Ordinal))
                {
                    failure = $"Crafting catalyst slot '{catalyst.slotId}' references an invalid item stack.";
                    return false;
                }

                requiredByInstance.TryGetValue(catalyst.itemInstanceId, out int reservedQuantity);
                requiredByInstance[catalyst.itemInstanceId] = reservedQuantity + catalyst.quantity;
            }

            foreach (KeyValuePair<string, int> required in requiredByInstance)
            {
                if (!itemRuntime.TryGetSnapshot(required.Key, out ItemInstanceSnapshot snapshot) || snapshot.StackQuantity < required.Value)
                {
                    failure = $"Item stack '{required.Key}' does not contain the {required.Value} items assigned across recipe and catalyst slots.";
                    return false;
                }
            }

            return true;
        }

        private static bool ConsumeInputs(
            CraftingOperationRecordData operation,
            ProductionRequirementPlanData plan,
            RecipeResolvedSnapshot recipe,
            ItemInstanceIdentityRuntime itemRuntime,
            out string failure)
        {
            failure = string.Empty;
            foreach (ProductionInputAllocationData allocation in Allocations(plan))
            {
                CraftingConsumedInputData consumed = new CraftingConsumedInputData
                {
                    allocationId = allocation.allocationId,
                    inputId = ResolveRecipeInputId(recipe, allocation.requirementId),
                    itemInstanceId = allocation.itemInstanceId,
                    definitionId = allocation.definitionId,
                    quantity = allocation.quantity,
                    unit = allocation.unit,
                    reusable = allocation.reusable,
                    consumed = false
                };

                if (!allocation.reusable && !string.IsNullOrWhiteSpace(allocation.itemInstanceId))
                {
                    if (allocation.unit != ProductionQuantityUnit.Count || Math.Abs(allocation.quantity - Math.Round(allocation.quantity)) > 0.0001f)
                    {
                        failure = $"Item input allocation '{allocation.allocationId}' must use a whole Count quantity.";
                        return false;
                    }

                    int quantity = Math.Max(1, (int)Math.Round(allocation.quantity));
                    string consumedId = DeterministicGuid($"{operation.operationId}:{allocation.allocationId}:consumed");
                    ItemInstanceOperationResult destroy = itemRuntime.ConsumeStackQuantity(allocation.itemInstanceId, quantity, consumedId);
                    if (!destroy.Succeeded)
                    {
                        failure = destroy.Message;
                        return false;
                    }

                    consumed.consumed = true;
                }

                operation.consumedInputs.Add(consumed);
            }

            return true;
        }

        private static bool ConsumeCatalysts(CraftingOperationRecordData operation, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            foreach (CraftingCatalystUseData catalyst in (operation.catalysts ?? new List<CraftingCatalystUseData>()).OrderBy(entry => entry.slotId, StringComparer.Ordinal))
            {
                string consumedId = DeterministicGuid($"{operation.operationId}:{catalyst.slotId}:catalyst-consumed");
                ItemInstanceOperationResult consume = itemRuntime.ConsumeStackQuantity(catalyst.itemInstanceId, catalyst.quantity, consumedId);
                if (!consume.Succeeded)
                {
                    failure = consume.Message;
                    return false;
                }

                catalyst.consumed = true;
            }

            return true;
        }

        private static bool CreateOutputs(
            CraftingOperationRecordData operation,
            RecipeResolvedSnapshot recipe,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            out string failure)
        {
            failure = string.Empty;
            if (recipe == null)
            {
                failure = "Resolved recipe snapshot is missing.";
                return false;
            }

            foreach (RecipeOutputSpecificationData output in recipe.Outputs.OrderBy(entry => entry.outputId, StringComparer.Ordinal))
            {
                CraftingOutputItemData record = new CraftingOutputItemData
                {
                    outputId = output.outputId,
                    itemDefinitionId = output.itemDefinitionId,
                    materialDefinitionId = output.materialDefinitionId,
                    outputKind = ToOutputKind(output.role),
                    quantity = output.quantity,
                    unit = output.unit
                };

                if (!string.IsNullOrWhiteSpace(output.itemDefinitionId))
                {
                    if (!registry.TryGet(output.itemDefinitionId, out ItemDefinition itemDefinition))
                    {
                        failure = $"Crafting output '{output.outputId}' references missing item definition '{output.itemDefinitionId}'.";
                        return false;
                    }

                    string itemInstanceId = DeterministicGuid($"{operation.operationId}:{output.outputId}:0");
                    if (output.unit != ProductionQuantityUnit.Count || output.quantity < 1f || Math.Abs(output.quantity - Math.Round(output.quantity)) > 0.0001f)
                    {
                        failure = $"Crafting output '{output.outputId}' must use a positive whole Count quantity when it creates an item.";
                        return false;
                    }

                    int outputQuantity = (int)Math.Round(output.quantity);
                    if (!itemDefinition.Stackable && outputQuantity != 1)
                    {
                        failure = $"Non-stackable crafting output '{output.outputId}' must produce exactly one item per output record.";
                        return false;
                    }

                    ItemInstanceClassification classification = itemDefinition.Stackable
                        ? ItemInstanceClassification.StackableWhileEquivalent
                        : ItemInstanceClassification.IndividuallyTracked;
                    ItemInstanceOperationResult create = itemRuntime.CreateItem(
                        itemDefinition,
                        classification,
                        itemInstanceId,
                        creatorPersonId: operation.actorPersonId,
                        ownerPersonId: operation.ownerPersonId,
                        custodianPersonId: operation.ownerPersonId,
                        creationSourceId: operation.operationId,
                        stackQuantity: outputQuantity);
                    if (!create.Succeeded)
                    {
                        failure = create.Message;
                        return false;
                    }

                    record.itemInstanceId = itemInstanceId;
                    record.createdItemInstance = true;
                    if (compositionRuntime != null)
                    {
                        ItemCompositionOperationResult composition = SetOutputComposition(recipe, output, operation, itemInstanceId, itemDefinition.Id, registry, itemRuntime, compositionRuntime, operation.operationId);
                        if (!composition.Succeeded)
                        {
                            failure = composition.Message;
                            return false;
                        }
                    }

                    if (qualityRuntime != null && classification == ItemInstanceClassification.IndividuallyTracked)
                    {
                        ItemQualityAffixOperationResult quality = qualityRuntime.EnsureDefaultQuality(itemRuntime, compositionRuntime, registry, itemInstanceId);
                        if (!quality.Succeeded)
                        {
                            failure = quality.Message;
                            return false;
                        }

                        if (TryResolvePolicy(registry, output.qualityPolicyId, CraftingOutputPolicyKind.QualityGeneration, out CraftingOutputPolicyDefinition qualityPolicy)
                            && qualityRuntime.TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot qualitySnapshot))
                        {
                            ItemQualityRecordData qualityRecord = qualitySnapshot.Data.Clone();
                            qualityRecord.overallQuality = Mathf.Clamp01(qualityPolicy.BaseQualityNormalized + operation.craftingQualityAdjustment);
                            qualityRecord.source = ItemQualityRecordSource.ProductionGenerated;
                            qualityRecord.generationPolicyId = qualityPolicy.Id;
                            qualityRecord.deterministicSeed = operation.deterministicSeed;
                            qualityRecord.provenanceId = operation.operationId;
                            foreach (ItemWorkmanshipEntryData entry in qualityRecord.workmanship)
                            {
                                entry.value ??= new ItemQualityValueData();
                                entry.value.state = QualityValueState.Known;
                                entry.value.value = qualityRecord.overallQuality;
                            }

                            foreach (ItemQualityDimensionEntryData entry in qualityRecord.dimensions)
                            {
                                entry.value ??= new ItemQualityValueData();
                                entry.value.state = QualityValueState.Known;
                                entry.value.value = qualityRecord.overallQuality;
                            }

                            quality = qualityRuntime.SetQualityRecord(itemRuntime, compositionRuntime, registry, qualityRecord);
                            if (!quality.Succeeded)
                            {
                                failure = quality.Message;
                                return false;
                            }
                        }

                        int requestedAffixes = output.affixPolicy is RecipeAffixPolicy.FixedAuthored or RecipeAffixPolicy.PoolReference or RecipeAffixPolicy.MaterialDerived ? 1 : 0;
                        if (output.affixPolicy == RecipeAffixPolicy.PolicyReference
                            && TryResolvePolicy(registry, output.affixPolicyId, CraftingOutputPolicyKind.AffixGeneration, out CraftingOutputPolicyDefinition affixPolicy))
                        {
                            float chance = Mathf.Clamp01(affixPolicy.AffixChance + operation.craftingAffixChanceBonus);
                            requestedAffixes = DeterministicUnitInterval($"{operation.deterministicSeed}:{itemInstanceId}:{affixPolicy.Id}") <= chance
                                ? affixPolicy.MaximumAffixCount
                                : 0;
                        }

                        ItemQualityAffixOperationResult affixes = qualityRuntime.GenerateAffixes(itemRuntime, compositionRuntime, registry, new ItemAffixGenerationRequest
                        {
                            ItemInstanceId = itemInstanceId,
                            PolicyId = output.affixPolicyId,
                            Seed = string.IsNullOrWhiteSpace(operation.deterministicSeed) ? operation.operationId : operation.deterministicSeed,
                            RequestedAffixCount = requestedAffixes,
                            Source = ItemAffixSource.Crafted,
                            CorrelationId = operation.operationId
                        });
                        if (!affixes.Succeeded)
                        {
                            failure = affixes.Message;
                            return false;
                        }
                    }

                    string craftedName = BuildCraftedDisplayName(recipe, output, operation, itemDefinition, registry);
                    ItemInstanceOperationResult rename = itemRuntime.Rename(itemInstanceId, craftedName);
                    if (!rename.Succeeded)
                    {
                        failure = rename.Message;
                        return false;
                    }

                    if (durabilityRuntime != null && classification == ItemInstanceClassification.IndividuallyTracked)
                    {
                        ItemDurabilityOperationResult durability = durabilityRuntime.EnsureDefaultDurability(itemRuntime, compositionRuntime, qualityRuntime, registry, itemInstanceId);
                        if (!durability.Succeeded)
                        {
                            failure = durability.Message;
                            return false;
                        }

                        if (TryResolvePolicy(registry, output.durabilityPolicyId, CraftingOutputPolicyKind.DurabilityInitialization, out CraftingOutputPolicyDefinition durabilityPolicy)
                            && durabilityRuntime.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot durabilitySnapshot))
                        {
                            ItemDurabilityRecordData durabilityRecord = durabilitySnapshot.Data.Clone();
                            durabilityRecord.currentDurability = durabilityRecord.maximumDurability * durabilityPolicy.InitialDurabilityNormalized;
                            durabilityRecord.policyId = durabilityPolicy.Id;
                            durabilityRecord.source = ItemDurabilityRecordSource.Generated;
                            durabilityRecord.provenanceId = operation.operationId;
                            foreach (ItemComponentDurabilityData component in durabilityRecord.components)
                            {
                                component.currentDurability = component.maximumDurability * durabilityPolicy.InitialDurabilityNormalized;
                            }

                            durability = durabilityRuntime.SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, durabilityRecord);
                            if (!durability.Succeeded)
                            {
                                failure = durability.Message;
                                return false;
                            }
                        }
                    }
                }

                operation.outputs.Add(record);
            }

            return operation.outputs.Count > 0;
        }

        private static bool ApplyCatalystEffects(
            CraftingOperationRecordData operation,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            out string failure)
        {
            failure = string.Empty;
            if (qualityRuntime == null || operation.catalysts == null || operation.catalysts.Count == 0)
            {
                return true;
            }

            CraftingOutputItemData[] targets = operation.outputs
                .Where(output => output.createdItemInstance
                    && output.outputKind == CraftingOutputKind.Primary
                    && qualityRuntime.TryGetQualityForItem(output.itemInstanceId, out _))
                .OrderBy(output => output.outputId, StringComparer.Ordinal)
                .ToArray();
            foreach (CraftingCatalystUseData catalyst in operation.catalysts.OrderBy(entry => entry.slotId, StringComparer.Ordinal))
            {
                if (!registry.TryGet(catalyst.itemDefinitionId, out ItemDefinition catalystDefinition))
                {
                    failure = $"Catalyst definition '{catalyst.itemDefinitionId}' is unavailable.";
                    return false;
                }

                CraftingCatalystEffectDefinition[] candidates = registry.DefinitionsById.Values
                    .OfType<CraftingCatalystEffectDefinition>()
                    .Where(effect => effect.Matches(catalystDefinition) && effect.GenerationWeight > 0f)
                    .OrderBy(effect => effect.Id, StringComparer.Ordinal)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                float selector = DeterministicUnitInterval($"{operation.deterministicSeed}:{catalyst.slotId}:effect");
                float totalWeight = candidates.Sum(effect => effect.GenerationWeight);
                float threshold = selector * totalWeight;
                CraftingCatalystEffectDefinition selected = candidates[^1];
                float cumulative = 0f;
                foreach (CraftingCatalystEffectDefinition candidate in candidates)
                {
                    cumulative += candidate.GenerationWeight;
                    if (threshold <= cumulative)
                    {
                        selected = candidate;
                        break;
                    }
                }

                catalyst.effectDefinitionId = selected.Id;
                catalyst.affixDefinitionId = selected.ResultingAffix?.Id ?? string.Empty;
                catalyst.affixTierId = selected.SelectAffixTierId(catalystDefinition, catalyst.quantity);
                catalyst.effectChance = selected.CalculateChance(catalystDefinition, catalyst.quantity, operation.craftingSkillUsed ? operation.craftingSkillGrade + 1 : 0);
                catalyst.effectRoll = DeterministicUnitInterval($"{operation.deterministicSeed}:{catalyst.slotId}:roll");
                if (catalyst.effectRoll > catalyst.effectChance)
                {
                    continue;
                }

                if (selected.ResultingAffix == null)
                {
                    failure = $"Catalyst effect '{selected.Id}' has no resulting affix.";
                    return false;
                }

                foreach (CraftingOutputItemData target in targets)
                {
                    ItemQualityAffixOperationResult applied = qualityRuntime.ApplyAffix(
                        itemRuntime,
                        compositionRuntime,
                        registry,
                        target.itemInstanceId,
                        selected.ResultingAffix,
                        catalyst.affixTierId,
                        seed: $"{operation.deterministicSeed}:{catalyst.slotId}:{target.itemInstanceId}",
                        source: ItemAffixSource.Crafted,
                        generationPolicyId: selected.Id);
                    if (!applied.Succeeded)
                    {
                        failure = applied.Message;
                        return false;
                    }
                }

                catalyst.effectApplied = targets.Length > 0;
            }

            return true;
        }

        private static ItemCompositionOperationResult SetOutputComposition(
            RecipeResolvedSnapshot recipe,
            RecipeOutputSpecificationData output,
            CraftingOperationRecordData operation,
            string itemInstanceId,
            string itemDefinitionId,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            string operationId)
        {
            List<(string componentId, string materialId, string sourceItemDefinitionId, float quantity, MaterialQuantityUnit unit, float weight)> materials = new List<(string componentId, string materialId, string sourceItemDefinitionId, float quantity, MaterialQuantityUnit unit, float weight)>();
            foreach (CraftingConsumedInputData input in operation?.consumedInputs ?? new List<CraftingConsumedInputData>())
            {
                if (string.IsNullOrWhiteSpace(input.definitionId) || input.quantity <= 0f)
                {
                    continue;
                }

                RecipeInputSpecificationData recipeInput = recipe?.Inputs.FirstOrDefault(candidate => string.Equals(candidate.inputId, input.inputId, StringComparison.Ordinal));
                string componentId = string.IsNullOrWhiteSpace(recipeInput?.componentRoleId) ? "component.main" : recipeInput.componentRoleId;
                if (registry.TryGet(input.definitionId, out MaterialDefinition materialDefinition))
                {
                    materials.Add((componentId, materialDefinition.Id, string.Empty, Math.Max(0.001f, input.quantity), ToMaterialUnit(input.unit), 1f));
                    continue;
                }

                if (registry.TryGet(input.definitionId, out ItemDefinition inputDefinition))
                {
                    foreach (ItemMaterialEntryData entry in inputDefinition.DefaultCompositionTemplate.materials ?? new List<ItemMaterialEntryData>())
                    {
                        if (!string.IsNullOrWhiteSpace(entry.materialDefinitionId))
                        {
                            float templateQuantity = entry.quantity == null ? 1f : Math.Max(0.001f, entry.quantity.value);
                            MaterialQuantityUnit unit = entry.quantity == null || entry.quantity.unit == MaterialQuantityUnit.Unknown
                                ? MaterialQuantityUnit.Count
                                : entry.quantity.unit;
                            bool proportional = unit is MaterialQuantityUnit.Ratio or MaterialQuantityUnit.Percent;
                            materials.Add((
                                componentId,
                                entry.materialDefinitionId,
                                inputDefinition.Id,
                                proportional ? templateQuantity : templateQuantity * Math.Max(0.001f, input.quantity),
                                unit,
                                proportional ? Math.Max(0.001f, input.quantity) : 1f));
                        }
                    }
                }
            }

            if (materials.Count == 0 && !string.IsNullOrWhiteSpace(output.materialDefinitionId))
            {
                string componentId = string.IsNullOrWhiteSpace(output.componentFoundationId) ? "component.main" : output.componentFoundationId;
                materials.Add((componentId, output.materialDefinitionId, string.Empty, Math.Max(0.001f, output.quantity), ToMaterialUnit(output.unit), 1f));
            }

            (string componentId, string materialId, string sourceItemDefinitionId, float quantity, MaterialQuantityUnit unit)[] combined = materials
                .Where(entry => !string.IsNullOrWhiteSpace(entry.materialId))
                .GroupBy(entry => (entry.componentId, entry.materialId, entry.sourceItemDefinitionId, entry.unit))
                .Select(group =>
                {
                    float quantity = group.Key.unit is MaterialQuantityUnit.Ratio or MaterialQuantityUnit.Percent
                        ? group.Sum(entry => entry.quantity * entry.weight) / group.Sum(entry => entry.weight)
                        : group.Sum(entry => entry.quantity);
                    return (group.Key.componentId, group.Key.materialId, group.Key.sourceItemDefinitionId, quantity, group.Key.unit);
                })
                .OrderBy(entry => entry.componentId, StringComparer.Ordinal)
                .ThenBy(entry => entry.materialId, StringComparer.Ordinal)
                .ThenBy(entry => entry.unit)
                .ToArray();
            if (combined.Length == 0)
            {
                return compositionRuntime.EnsureCompositionForItem(itemRuntime, registry, itemInstanceId);
            }

            ItemCompositionRecordData composition = new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{itemInstanceId}",
                itemInstanceId = itemInstanceId,
                sourceItemDefinitionId = itemDefinitionId,
                completeness = ItemCompositionCompleteness.Complete,
                source = operationId,
                massAuthority = ItemCompositionMassAuthority.CompositionProjection,
                lastMutationPurpose = ItemCompositionMutationPurpose.CraftingProduction,
                provenanceIds = new[] { operationId },
                tags = new[] { "item.composition", "composition.crafted" }
            };
            Dictionary<string, List<string>> materialIdsByComponent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < combined.Length; i++)
            {
                string entryId = $"material.{output.outputId}.{i + 1}";
                if (!materialIdsByComponent.TryGetValue(combined[i].componentId, out List<string> componentMaterialIds))
                {
                    componentMaterialIds = new List<string>();
                    materialIdsByComponent.Add(combined[i].componentId, componentMaterialIds);
                }

                composition.materials.Add(new ItemMaterialEntryData
                {
                    entryId = entryId,
                    materialDefinitionId = combined[i].materialId,
                    sourceItemDefinitionId = combined[i].sourceItemDefinitionId,
                    role = componentMaterialIds.Count == 0 ? MaterialEntryRole.PrimaryStructure : MaterialEntryRole.Binding,
                    quantity = new MaterialQuantityData { value = combined[i].quantity, unit = combined[i].unit },
                    purity = 1f,
                    componentEntryId = combined[i].componentId
                });
                componentMaterialIds.Add(entryId);
            }

            foreach (KeyValuePair<string, List<string>> component in materialIdsByComponent.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                composition.components.Add(new ItemComponentEntryData
                {
                    componentEntryId = component.Key,
                    componentName = DisplayComponentName(component.Key),
                    kind = ItemComponentKind.AbstractComponent,
                    materialEntryIds = component.Value.ToArray()
                });
            }
            return compositionRuntime.SetComposition(itemRuntime, registry, composition, ItemCompositionMutationPurpose.CraftingProduction);
        }

        private static string DisplayComponentName(string componentId)
        {
            string suffix = (componentId ?? string.Empty).Split('.').LastOrDefault() ?? "Component";
            return string.Join(" ", suffix.Split('-', '_').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string BuildCraftedDisplayName(
            RecipeResolvedSnapshot recipe,
            RecipeOutputSpecificationData output,
            CraftingOperationRecordData operation,
            ItemDefinition outputDefinition,
            DefinitionRegistry registry)
        {
            RecipeInputSpecificationData[] inputs = (recipe?.Inputs ?? Array.Empty<RecipeInputSpecificationData>()).ToArray();
            RecipeInputSpecificationData primary = FindNamingInput(inputs, "blade", "shell", "face", "limbs", "shafts") ?? inputs.FirstOrDefault();
            RecipeInputSpecificationData secondary = FindNamingInput(inputs, "hilt", "grip", "handle", "lining", "rim", "fittings", "heads", "guard", "pommel");
            if (ReferenceEquals(primary, secondary)) secondary = inputs.FirstOrDefault(input => !ReferenceEquals(input, primary));

            string craftableTag = string.Empty;
            if (registry != null)
            {
                craftableTag = registry.DefinitionsById.Values
                    .OfType<RecipeDefinition>()
                    .FirstOrDefault(candidate => string.Equals(candidate.Id, recipe?.RecipeId, StringComparison.Ordinal))?
                    .Tags.FirstOrDefault(tag => tag.StartsWith("craftable.", StringComparison.Ordinal)) ?? string.Empty;
            }

            string baseItemName = string.IsNullOrWhiteSpace(craftableTag)
                ? (outputDefinition?.DisplayName ?? output?.itemDefinitionId ?? "Item").Replace("Prototype ", string.Empty)
                : DisplayComponentName(craftableTag);
            if (output != null && output.quantity > 1f && !baseItemName.EndsWith("s", StringComparison.OrdinalIgnoreCase)) baseItemName += "s";
            string primaryMaterial = ResolveInputMaterialName(primary, operation, registry);
            if (string.IsNullOrWhiteSpace(primaryMaterial)
                && output != null
                && registry.TryGet(output.materialDefinitionId, out MaterialDefinition fallbackMaterial))
            {
                primaryMaterial = NormalizeCraftedMaterialName(fallbackMaterial.DisplayName);
            }

            string name = string.IsNullOrWhiteSpace(primaryMaterial) ? baseItemName : $"{primaryMaterial} {baseItemName}";
            string secondaryMaterial = ResolveInputMaterialName(secondary, operation, registry);
            if (!string.IsNullOrWhiteSpace(secondaryMaterial) && secondary != null)
            {
                string componentName = DisplayComponentName(secondary.componentRoleId);
                if (string.Equals(componentName, "Hilt", StringComparison.Ordinal)) componentName = "Grip";
                name += $" with {secondaryMaterial} {componentName}";
            }

            return name;
        }

        private static RecipeInputSpecificationData FindNamingInput(IEnumerable<RecipeInputSpecificationData> inputs, params string[] componentNames)
        {
            foreach (string componentName in componentNames)
            {
                RecipeInputSpecificationData found = (inputs ?? Array.Empty<RecipeInputSpecificationData>())
                    .FirstOrDefault(input => (input?.componentRoleId ?? string.Empty).EndsWith($".{componentName}", StringComparison.Ordinal));
                if (found != null) return found;
            }

            return null;
        }

        private static string ResolveInputMaterialName(RecipeInputSpecificationData input, CraftingOperationRecordData operation, DefinitionRegistry registry)
        {
            if (input == null || operation == null || registry == null) return string.Empty;
            CraftingConsumedInputData consumed = (operation.consumedInputs ?? new List<CraftingConsumedInputData>())
                .FirstOrDefault(candidate => string.Equals(candidate.inputId, input.inputId, StringComparison.Ordinal));
            if (consumed == null) return string.Empty;
            if (registry.TryGet(consumed.definitionId, out MaterialDefinition material)) return NormalizeCraftedMaterialName(material.DisplayName);
            if (registry.TryGet(consumed.definitionId, out ItemDefinition item))
            {
                string materialId = item.DefaultCompositionTemplate.materials?
                    .FirstOrDefault(entry => entry != null && !string.IsNullOrWhiteSpace(entry.materialDefinitionId))?.materialDefinitionId;
                if (registry.TryGet(materialId, out MaterialDefinition composedMaterial)) return NormalizeCraftedMaterialName(composedMaterial.DisplayName);
            }

            return string.Empty;
        }

        private static string NormalizeCraftedMaterialName(string value)
        {
            string name = value?.Trim() ?? string.Empty;
            foreach (string suffix in new[] { " Ore", " Ingot", " Log" })
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - suffix.Length);
            }

            return name;
        }

        private static string ResolveRecipeInputId(RecipeResolvedSnapshot recipe, string requirementId)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(requirementId))
            {
                return requirementId ?? string.Empty;
            }

            string prefix = $"production-requirement.recipe.{recipe.RecipeId}.";
            if (requirementId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return requirementId.Substring(prefix.Length);
            }

            return requirementId;
        }

        private static bool ApplyToolWear(
            CraftingOperationRecordData operation,
            ProductionRequirementPlanData plan,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            DefinitionRegistry registry,
            out string failure)
        {
            failure = string.Empty;
            if (durabilityRuntime == null)
            {
                return true;
            }

            foreach (ProductionRequirementSelectionData selection in (plan?.selections ?? new List<ProductionRequirementSelectionData>()).OrderBy(entry => entry.requirementId, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(selection.selectedToolItemInstanceId) || selection.expectedToolWear <= 0f)
                {
                    continue;
                }

                ItemDurabilityOperationResult wear = durabilityRuntime.ApplyWear(itemRuntime, compositionRuntime, qualityRuntime, registry, selection.selectedToolItemInstanceId, selection.expectedToolWear, operation.operationId);
                if (!wear.Succeeded)
                {
                    failure = wear.Message;
                    return false;
                }

                operation.toolUses.Add(new CraftingToolUseData
                {
                    requirementId = selection.requirementId,
                    toolItemInstanceId = selection.selectedToolItemInstanceId,
                    toolDefinitionId = selection.selectedToolDefinitionId,
                    wearApplied = selection.expectedToolWear,
                    applied = true
                });
            }

            return true;
        }

        private CraftingExecutionResult FailAndRollback(
            CraftingExecutionStatus status,
            string message,
            CraftingOperationRecordData operation,
            RecipeResolutionResult recipeResult,
            RuntimeRollbackSnapshot rollback,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            ProductionRequirementRuntime productionRuntime)
        {
            operation.state = CraftingOperationState.Failed;
            operation.status = status;
            operation.diagnostics = AddDiagnostics(operation.diagnostics, message);
            if (rollback == null)
            {
                return CraftingExecutionResult.Failure(status, message, operation, recipeResult);
            }

            if (!rollback.Restore(registry, itemRuntime, compositionRuntime, qualityRuntime, durabilityRuntime, productionRuntime, this, out string rollbackFailure))
            {
                operation.state = CraftingOperationState.Failed;
                operation.status = CraftingExecutionStatus.RollbackFailed;
                operation.diagnostics = AddDiagnostics(operation.diagnostics, rollbackFailure);
                return CraftingExecutionResult.Failure(CraftingExecutionStatus.RollbackFailed, $"{message} Rollback failed: {rollbackFailure}", operation, recipeResult);
            }

            operation.state = CraftingOperationState.RolledBack;
            return CraftingExecutionResult.Failure(status, message, operation, recipeResult);
        }

        private static IEnumerable<ProductionInputAllocationData> Allocations(ProductionRequirementPlanData plan)
        {
            return (plan?.selections ?? new List<ProductionRequirementSelectionData>())
                .SelectMany(selection => selection.allocations ?? new List<ProductionInputAllocationData>())
                .OrderBy(allocation => allocation.requirementId, StringComparer.Ordinal)
                .ThenBy(allocation => allocation.allocationId, StringComparer.Ordinal);
        }

        private static CraftingOutputKind ToOutputKind(RecipeOutputRole role)
        {
            return role switch
            {
                RecipeOutputRole.SecondaryOutput => CraftingOutputKind.Secondary,
                RecipeOutputRole.Byproduct => CraftingOutputKind.Byproduct,
                RecipeOutputRole.Waste => CraftingOutputKind.Waste,
                RecipeOutputRole.Scrap => CraftingOutputKind.Scrap,
                RecipeOutputRole.RecoveredInput => CraftingOutputKind.RecoveredInput,
                RecipeOutputRole.FailedResult or RecipeOutputRole.DamagedResult => CraftingOutputKind.FailureOutput,
                _ => CraftingOutputKind.Primary
            };
        }

        private static MaterialQuantityUnit ToMaterialUnit(ProductionQuantityUnit unit)
        {
            return unit switch
            {
                ProductionQuantityUnit.Kilogram => MaterialQuantityUnit.Kilogram,
                ProductionQuantityUnit.Liter => MaterialQuantityUnit.Liter,
                _ => MaterialQuantityUnit.Count
            };
        }

        private static string[] AddDiagnostics(IEnumerable<string> existing, string message)
        {
            return (existing ?? Array.Empty<string>())
                .Concat(new[] { message ?? string.Empty })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void NormalizeRequest(CraftingExecutionRequest request)
        {
            request.operationId = string.IsNullOrWhiteSpace(request.operationId) ? $"crafting-operation.{DeterministicGuid($"{request.recipeId}:{request.worldTime}:{request.deterministicSeed}")}" : request.operationId.Trim();
            request.batchSize = request.batchSize <= 0f ? 1f : request.batchSize;
            request.actorPersonId = request.actorPersonId ?? string.Empty;
            request.ownerPersonId = string.IsNullOrWhiteSpace(request.ownerPersonId) ? request.actorPersonId : request.ownerPersonId;
            request.custodianPersonId = string.IsNullOrWhiteSpace(request.custodianPersonId) ? request.ownerPersonId : request.custodianPersonId;
        }

        private static string DeterministicGuid(string seed)
        {
            using MD5 md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("D");
        }

        private static float DeterministicUnitInterval(string seed)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            uint value = BitConverter.ToUInt32(bytes, 0);
            return value / (float)uint.MaxValue;
        }

        private static bool TryResolvePolicy(
            DefinitionRegistry registry,
            string policyId,
            CraftingOutputPolicyKind expectedKind,
            out CraftingOutputPolicyDefinition policy)
        {
            policy = null;
            return !string.IsNullOrWhiteSpace(policyId)
                && registry != null
                && registry.TryGet(policyId, out policy)
                && policy.Kind == expectedKind;
        }

        private sealed class RuntimeRollbackSnapshot
        {
            private ItemInstanceRuntimeSaveData itemInstances;
            private ItemCompositionRuntimeSaveData itemCompositions;
            private ItemQualityAffixRuntimeSaveData itemQuality;
            private ItemDurabilityRuntimeSaveData itemDurability;
            private ProductionRequirementRuntimeSaveData production;
            private CraftingExecutionRuntimeSaveData crafting;

            public static RuntimeRollbackSnapshot Capture(
                ItemInstanceIdentityRuntime itemRuntime,
                ItemCompositionRuntime compositionRuntime,
                ItemQualityAffixRuntime qualityRuntime,
                ItemDurabilityRuntime durabilityRuntime,
                ProductionRequirementRuntime productionRuntime,
                CraftingExecutionRuntime craftingRuntime)
            {
                return new RuntimeRollbackSnapshot
                {
                    itemInstances = itemRuntime?.CreateSaveData(),
                    itemCompositions = compositionRuntime?.CreateSaveData(),
                    itemQuality = qualityRuntime?.CreateSaveData(),
                    itemDurability = durabilityRuntime?.CreateSaveData(),
                    production = productionRuntime?.CreateSaveData(),
                    crafting = craftingRuntime?.CreateSaveData()
                };
            }

            public bool Restore(
                DefinitionRegistry registry,
                ItemInstanceIdentityRuntime itemRuntime,
                ItemCompositionRuntime compositionRuntime,
                ItemQualityAffixRuntime qualityRuntime,
                ItemDurabilityRuntime durabilityRuntime,
                ProductionRequirementRuntime productionRuntime,
                CraftingExecutionRuntime craftingRuntime,
                out string failure)
            {
                failure = string.Empty;
                if (itemRuntime != null && itemInstances != null)
                {
                    ItemInstanceOperationResult result = itemRuntime.RestoreFromSaveData(itemInstances, registry);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                if (compositionRuntime != null && itemCompositions != null)
                {
                    ItemCompositionOperationResult result = compositionRuntime.RestoreFromSaveData(itemCompositions, registry, itemRuntime);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                if (qualityRuntime != null && itemQuality != null)
                {
                    ItemQualityAffixOperationResult result = qualityRuntime.RestoreFromSaveData(itemQuality, registry, itemRuntime);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                if (durabilityRuntime != null && itemDurability != null)
                {
                    ItemDurabilityOperationResult result = durabilityRuntime.RestoreFromSaveData(itemDurability, registry, itemRuntime, compositionRuntime);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                if (productionRuntime != null && production != null)
                {
                    ProductionRequirementEvaluationResult result = productionRuntime.RestoreFromSaveData(production);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                if (craftingRuntime != null && crafting != null)
                {
                    CraftingExecutionResult result = craftingRuntime.RestoreFromSaveData(crafting, registry);
                    if (!result.Succeeded)
                    {
                        failure = result.Message;
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
