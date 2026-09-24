using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Durability;

namespace UnityIsekaiGame.Inventory.Recipes
{
    [Serializable]
    public sealed class RecipeResolutionRequest
    {
        public string recipeId;
        public string versionId;
        public string variantId;
        public float batchSize = 1f;
        public string[] selectedOptionalInputIds = Array.Empty<string>();
        public RecipeProjectionAccessLevel accessLevel = RecipeProjectionAccessLevel.Privileged;
        public ProductionContextData productionContext = new ProductionContextData();
        public bool buildRequirementPlan = true;
        public bool reservePlan;
        public string productionJobId;
        public string planId;
        public string reservationExpiresWorldTime;

        public RecipeResolutionRequest Clone()
        {
            return new RecipeResolutionRequest
            {
                recipeId = recipeId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                batchSize = batchSize,
                selectedOptionalInputIds = CloneIds(selectedOptionalInputIds),
                accessLevel = accessLevel,
                productionContext = productionContext?.Clone() ?? new ProductionContextData(),
                buildRequirementPlan = buildRequirementPlan,
                reservePlan = reservePlan,
                productionJobId = productionJobId ?? string.Empty,
                planId = planId ?? string.Empty,
                reservationExpiresWorldTime = reservationExpiresWorldTime ?? string.Empty
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class RecipeResolutionResult
    {
        private RecipeResolutionResult(bool succeeded, bool preview, RecipeResolutionStatus status, string message, RecipeResolvedSnapshot snapshot, ProductionRequirementEvaluationResult requirementResult, ProductionReservationResult reservationResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot?.Clone();
            RequirementResult = requirementResult;
            ReservationResult = reservationResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public RecipeResolutionStatus Status { get; }
        public string Message { get; }
        public RecipeResolvedSnapshot Snapshot { get; }
        public ProductionRequirementEvaluationResult RequirementResult { get; }
        public ProductionReservationResult ReservationResult { get; }

        public static RecipeResolutionResult Success(RecipeResolvedSnapshot snapshot, string message, bool preview = true, ProductionRequirementEvaluationResult requirementResult = null, ProductionReservationResult reservationResult = null)
        {
            return new RecipeResolutionResult(true, preview, preview ? RecipeResolutionStatus.Preview : RecipeResolutionStatus.Succeeded, message, snapshot, requirementResult, reservationResult);
        }

        public static RecipeResolutionResult Failure(RecipeResolutionStatus status, string message, RecipeResolvedSnapshot snapshot = null, ProductionRequirementEvaluationResult requirementResult = null)
        {
            return new RecipeResolutionResult(false, false, status, message, snapshot, requirementResult, null);
        }
    }

    public sealed class RecipeRuntime
    {
        public RecipeResolutionResult Resolve(
            RecipeResolutionRequest request,
            DefinitionRegistry registry,
            ProductionRequirementRuntime productionRuntime = null,
            ItemInstanceIdentityRuntime itemRuntime = null,
            ItemDurabilityRuntime durabilityRuntime = null)
        {
            RecipeResolutionRequest working = request?.Clone() ?? new RecipeResolutionRequest();
            if (registry == null)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.MissingRecipe, "Definition registry is required for recipe resolution.");
            }

            if (string.IsNullOrWhiteSpace(working.recipeId) || !registry.TryGet(working.recipeId, out RecipeDefinition recipe))
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.MissingRecipe, $"Recipe '{working.recipeId}' was not found.");
            }

            if (recipe.State == RecipeLifecycleState.Disabled)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.ValidationFailed, $"Recipe '{recipe.Id}' is disabled.");
            }

            if (!TryResolveVersion(recipe, working.versionId, out RecipeVersionData version, out string versionFailure))
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.MissingVersion, versionFailure);
            }

            if (!TryResolveVariant(recipe, working.variantId, out RecipeVariantData variant, out string variantFailure))
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.MissingVariant, variantFailure);
            }

            if (!TryResolveBatchFactor(recipe.BatchPolicy, working.batchSize, out float batchFactor, out string batchFailure))
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.InvalidBatch, batchFailure);
            }

            HashSet<string> selectedOptional = new HashSet<string>(working.selectedOptionalInputIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            bool privileged = working.accessLevel == RecipeProjectionAccessLevel.Privileged;
            RecipeInputSpecificationData[] inputs = BuildInputs(recipe, variant, selectedOptional, privileged, batchFactor);
            RecipeOutputSpecificationData[] outputs = BuildOutputs(recipe, variant, privileged, batchFactor);
            RecipeProcedureStepData[] steps = BuildProcedure(recipe, privileged);
            RecipeTransferMappingData[] transfers = BuildTransfers(recipe, privileged);

            string[] requirementIds = BuildRequirementIds(recipe, variant);
            RecipeResolvedSnapshot snapshot = new RecipeResolvedSnapshot(
                recipe.Id,
                version.versionId,
                variant?.variantId ?? string.Empty,
                working.batchSize <= 0f ? recipe.BatchPolicy.baseBatchSize : working.batchSize,
                inputs,
                outputs,
                transfers,
                steps,
                requirementIds,
                !privileged,
                ComputeSnapshotSignature(recipe.Id, version.versionId, variant?.variantId ?? string.Empty, working.batchSize, inputs, outputs, transfers, steps, requirementIds));

            if (!working.buildRequirementPlan)
            {
                return RecipeResolutionResult.Success(snapshot, "Recipe preview resolved.", preview: true);
            }

            if (productionRuntime == null)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.RequirementFailed, "Production requirement runtime is required to build recipe plans.", snapshot);
            }

            List<ProductionRequirementDefinition> requirements = BuildRequirements(recipe, variant, inputs, registry);
            if (requirements.Count == 0)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.RequirementFailed, "Recipe produced no production requirements.", snapshot);
            }

            string jobId = string.IsNullOrWhiteSpace(working.productionJobId) ? $"recipe-job.{recipe.Id}.{version.versionId}" : working.productionJobId;
            ProductionRequirementEvaluationResult evaluation = productionRuntime.EvaluateRequirements(
                requirements,
                working.productionContext ?? new ProductionContextData(),
                registry,
                itemRuntime,
                durabilityRuntime,
                jobId,
                preview: !working.reservePlan,
                planId: string.IsNullOrWhiteSpace(working.planId) ? string.Empty : working.planId);

            if (evaluation == null || !evaluation.Succeeded)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.RequirementFailed, evaluation?.Message ?? "Recipe production requirement plan failed.", snapshot, evaluation);
            }

            if (!working.reservePlan)
            {
                return RecipeResolutionResult.Success(snapshot, "Recipe preview and requirement plan resolved.", preview: true, requirementResult: evaluation);
            }

            ProductionReservationResult reservation = productionRuntime.ReservePlan(evaluation.Plan?.planId, working.reservationExpiresWorldTime);
            if (reservation == null || !reservation.Succeeded)
            {
                return RecipeResolutionResult.Failure(RecipeResolutionStatus.RequirementFailed, reservation?.Message ?? "Recipe reservation failed.", snapshot, evaluation);
            }

            return RecipeResolutionResult.Success(snapshot, "Recipe plan reserved.", preview: false, requirementResult: evaluation, reservationResult: reservation);
        }

        private static RecipeInputSpecificationData[] BuildInputs(RecipeDefinition recipe, RecipeVariantData variant, HashSet<string> selectedOptional, bool privileged, float batchFactor)
        {
            IEnumerable<RecipeInputSpecificationData> baseInputs = recipe.Inputs;
            IEnumerable<RecipeInputSpecificationData> variantInputs = variant?.additionalInputs ?? Array.Empty<RecipeInputSpecificationData>();
            return baseInputs.Concat(variantInputs)
                .Where(input => input != null)
                .Where(input => input.requirementState != RecipeRequirementState.Optional || selectedOptional.Contains(input.inputId))
                .Select(input => ProjectInput(input.CloneScaled(batchFactor), privileged))
                .OrderBy(input => input.inputId, StringComparer.Ordinal)
                .ToArray();
        }

        private static RecipeOutputSpecificationData[] BuildOutputs(RecipeDefinition recipe, RecipeVariantData variant, bool privileged, float batchFactor)
        {
            IEnumerable<RecipeOutputSpecificationData> baseOutputs = recipe.Outputs;
            IEnumerable<RecipeOutputSpecificationData> variantOutputs = variant?.outputOverrides ?? Array.Empty<RecipeOutputSpecificationData>();
            return baseOutputs.Concat(variantOutputs)
                .Where(output => output != null)
                .Select(output => ProjectOutput(output.CloneScaled(batchFactor), privileged))
                .OrderBy(output => output.outputId, StringComparer.Ordinal)
                .ToArray();
        }

        private static RecipeProcedureStepData[] BuildProcedure(RecipeDefinition recipe, bool privileged)
        {
            return TopologicalSort(recipe.ProcedureSteps)
                .Select(step => ProjectStep(step, privileged))
                .ToArray();
        }

        private static RecipeTransferMappingData[] BuildTransfers(RecipeDefinition recipe, bool privileged)
        {
            return recipe.TransferMappings
                .Where(mapping => mapping != null)
                .Select(mapping => ProjectTransfer(mapping, privileged))
                .OrderBy(mapping => mapping.sourceInputId, StringComparer.Ordinal)
                .ThenBy(mapping => mapping.targetOutputId, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] BuildRequirementIds(RecipeDefinition recipe, RecipeVariantData variant)
        {
            return (recipe.RecipeRequirementIds ?? Array.Empty<string>())
                .Concat(variant?.additionalRequirementIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool TryResolveVersion(RecipeDefinition recipe, string requestedVersionId, out RecipeVersionData version, out string failure)
        {
            string versionId = string.IsNullOrWhiteSpace(requestedVersionId) ? recipe.CurrentVersionId : requestedVersionId;
            version = recipe.Versions.FirstOrDefault(entry => string.Equals(entry.versionId, versionId, StringComparison.Ordinal));
            if (version != null)
            {
                failure = string.Empty;
                return true;
            }

            failure = $"Recipe '{recipe.Id}' does not contain version '{versionId}'.";
            return false;
        }

        private static bool TryResolveVariant(RecipeDefinition recipe, string requestedVariantId, out RecipeVariantData variant, out string failure)
        {
            if (string.IsNullOrWhiteSpace(requestedVariantId))
            {
                variant = null;
                failure = string.Empty;
                return true;
            }

            variant = recipe.Variants.FirstOrDefault(entry => string.Equals(entry.variantId, requestedVariantId, StringComparison.Ordinal));
            if (variant != null)
            {
                failure = string.Empty;
                return true;
            }

            failure = $"Recipe '{recipe.Id}' does not contain variant '{requestedVariantId}'.";
            return false;
        }

        private static bool TryResolveBatchFactor(RecipeBatchPolicyData policy, float requestedBatchSize, out float factor, out string failure)
        {
            RecipeBatchPolicyData batch = policy ?? new RecipeBatchPolicyData();
            float size = requestedBatchSize <= 0f ? batch.baseBatchSize : requestedBatchSize;
            if (size < batch.minimumBatchSize || size > batch.maximumBatchSize)
            {
                factor = 0f;
                failure = $"Batch size {size} is outside recipe bounds {batch.minimumBatchSize}-{batch.maximumBatchSize}.";
                return false;
            }

            if (batch.scalingPolicy == RecipeBatchScalingPolicy.Discrete && batch.batchIncrement > 0f)
            {
                float offset = size - batch.minimumBatchSize;
                float steps = offset / batch.batchIncrement;
                if (Math.Abs(steps - (float)Math.Round(steps)) > 0.0001f)
                {
                    factor = 0f;
                    failure = $"Batch size {size} does not align to increment {batch.batchIncrement}.";
                    return false;
                }
            }

            factor = batch.scalingPolicy == RecipeBatchScalingPolicy.NoScaling ? 1f : size / Math.Max(0.0001f, batch.baseBatchSize);
            failure = string.Empty;
            return true;
        }

        private static RecipeInputSpecificationData ProjectInput(RecipeInputSpecificationData input, bool privileged)
        {
            if (input == null)
            {
                return null;
            }

            if (privileged || !input.hidden)
            {
                input.selected = true;
                return input;
            }

            return new RecipeInputSpecificationData
            {
                inputId = input.inputId,
                role = input.role,
                requirementState = input.requirementState,
                classification = input.classification,
                quantity = input.quantity,
                unit = input.unit,
                hidden = true,
                selected = input.selected,
                accessPolicyId = Redact(input.accessPolicyId)
            };
        }

        private static RecipeOutputSpecificationData ProjectOutput(RecipeOutputSpecificationData output, bool privileged)
        {
            if (output == null || privileged)
            {
                return output;
            }

            return output.CloneScaled(1f);
        }

        private static RecipeProcedureStepData ProjectStep(RecipeProcedureStepData step, bool privileged)
        {
            if (step == null || privileged || !step.hidden)
            {
                return step?.Clone();
            }

            return new RecipeProcedureStepData
            {
                stepId = step.stepId,
                stepKind = RecipeProcedureStepKind.Custom,
                displayName = "Redacted Step",
                hidden = true,
                repeatCount = Math.Max(1, step.repeatCount)
            };
        }

        private static RecipeTransferMappingData ProjectTransfer(RecipeTransferMappingData mapping, bool privileged)
        {
            if (mapping == null || privileged)
            {
                return mapping?.Clone();
            }

            RecipeTransferMappingData clone = mapping.Clone();
            if (clone.preserveTrackedComponent)
            {
                clone.targetComponentId = Redact(clone.targetComponentId);
            }

            return clone;
        }

        private static IReadOnlyList<RecipeProcedureStepData> TopologicalSort(IEnumerable<RecipeProcedureStepData> steps)
        {
            Dictionary<string, RecipeProcedureStepData> byId = (steps ?? Array.Empty<RecipeProcedureStepData>())
                .Where(step => step != null && !string.IsNullOrWhiteSpace(step.stepId))
                .GroupBy(step => step.stepId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Clone(), StringComparer.Ordinal);
            List<RecipeProcedureStepData> ordered = new List<RecipeProcedureStepData>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in byId.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                Visit(id);
            }

            return ordered;

            void Visit(string id)
            {
                if (visited.Contains(id) || !byId.TryGetValue(id, out RecipeProcedureStepData step))
                {
                    return;
                }

                if (!visiting.Add(id))
                {
                    return;
                }

                foreach (string dependency in (step.dependsOnStepIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal))
                {
                    Visit(dependency);
                }

                visiting.Remove(id);
                visited.Add(id);
                ordered.Add(step.Clone());
            }
        }

        private static List<ProductionRequirementDefinition> BuildRequirements(RecipeDefinition recipe, RecipeVariantData variant, IReadOnlyList<RecipeInputSpecificationData> inputs, DefinitionRegistry registry)
        {
            List<ProductionRequirementDefinition> requirements = new List<ProductionRequirementDefinition>();
            foreach (string requirementId in BuildRequirementIds(recipe, variant))
            {
                if (registry.TryGet(requirementId, out ProductionRequirementDefinition requirement))
                {
                    requirements.Add(requirement);
                }
            }

            foreach (RecipeInputSpecificationData input in inputs ?? Array.Empty<RecipeInputSpecificationData>())
            {
                if (input == null || input.classification == RecipeInputClassification.StationProvided || input.quantity <= 0f)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(input.itemDefinitionId) && registry.TryGet(input.itemDefinitionId, out ItemDefinition item))
                {
                    requirements.Add(CreateTransientRequirement($"production-requirement.recipe.{recipe.Id}.{input.inputId}", ProductionRequirementType.Item, input.quantity, input.unit, item, null, input.requirementState));
                }
                else if ((input.itemCategoryIds?.Length ?? 0) > 0 || (input.materialTagIds?.Length ?? 0) > 0)
                {
                    requirements.Add(CreateTransientItemFamilyRequirement(
                        $"production-requirement.recipe.{recipe.Id}.{input.inputId}",
                        input.quantity,
                        input.unit,
                        RecipeInputMatcher.FindMatchingItems(input, registry),
                        input.requirementState));
                }
                else if (!string.IsNullOrWhiteSpace(input.materialDefinitionId) && registry.TryGet(input.materialDefinitionId, out MaterialDefinition material))
                {
                    requirements.Add(CreateTransientRequirement($"production-requirement.recipe.{recipe.Id}.{input.inputId}", ProductionRequirementType.Material, input.quantity, input.unit, null, material, input.requirementState));
                }
            }

            return requirements.OrderBy(requirement => requirement.Priority).ThenBy(requirement => requirement.Id, StringComparer.Ordinal).ToList();
        }

        private static ProductionRequirementDefinition CreateTransientItemFamilyRequirement(
            string id,
            float quantity,
            ProductionQuantityUnit unit,
            IReadOnlyList<ItemDefinition> candidates,
            RecipeRequirementState state)
        {
            ItemDefinition[] ordered = (candidates ?? Array.Empty<ItemDefinition>())
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray();
            ProductionRequirementDefinition requirement = CreateTransientRequirement(
                id,
                ProductionRequirementType.Item,
                quantity,
                unit,
                ordered.FirstOrDefault(),
                null,
                state);
            ProductionRequirementAlternativeDefinition[] alternatives = ordered.Skip(1).Select(candidate =>
            {
                ProductionRequirementAlternativeDefinition alternative = new ProductionRequirementAlternativeDefinition();
                SetField(alternative, "requirementType", ProductionRequirementType.Item);
                SetField(alternative, "itemDefinition", candidate);
                SetField(alternative, "quantity", quantity);
                SetField(alternative, "quantityUnit", unit);
                return alternative;
            }).ToArray();
            SetField(requirement, "alternatives", alternatives);
            return requirement;
        }

        private static ProductionRequirementDefinition CreateTransientRequirement(string id, ProductionRequirementType type, float quantity, ProductionQuantityUnit unit, ItemDefinition item, MaterialDefinition material, RecipeRequirementState state)
        {
            ProductionRequirementDefinition requirement = ScriptableObject.CreateInstance<ProductionRequirementDefinition>();
            requirement.hideFlags = HideFlags.HideAndDontSave;
            SetField(requirement, "requirementId", id);
            SetField(requirement, "displayName", id);
            SetField(requirement, "requirementGroupId", "requirement-group.recipe-inputs");
            SetField(requirement, "requirementType", type);
            SetField(requirement, "strictness", state == RecipeRequirementState.Optional ? ProductionRequirementStrictness.Optional : ProductionRequirementStrictness.Required);
            SetField(requirement, "allowSubstitution", true);
            SetField(requirement, "itemDefinition", item);
            SetField(requirement, "materialDefinition", material);
            SetField(requirement, "quantity", quantity);
            SetField(requirement, "quantityUnit", unit);
            return requirement;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static string ComputeSnapshotSignature(string recipeId, string versionId, string variantId, float batchSize, IReadOnlyList<RecipeInputSpecificationData> inputs, IReadOnlyList<RecipeOutputSpecificationData> outputs, IReadOnlyList<RecipeTransferMappingData> transfers, IReadOnlyList<RecipeProcedureStepData> steps, IReadOnlyList<string> requirementIds)
        {
            string payload = string.Join("|", new[]
            {
                recipeId ?? string.Empty,
                versionId ?? string.Empty,
                variantId ?? string.Empty,
                batchSize.ToString("0.###"),
                string.Join(",", (inputs ?? Array.Empty<RecipeInputSpecificationData>()).Select(input => $"{input.inputId}:{input.itemDefinitionId}:{input.materialDefinitionId}:{string.Join("+", input.itemCategoryIds ?? Array.Empty<string>())}:{string.Join("+", input.materialTagIds ?? Array.Empty<string>())}:{input.componentRoleId}:{input.quantity:0.###}:{input.hidden}")),
                string.Join(",", (outputs ?? Array.Empty<RecipeOutputSpecificationData>()).Select(output => $"{output.outputId}:{output.itemDefinitionId}:{output.materialDefinitionId}:{output.quantity:0.###}")),
                string.Join(",", (transfers ?? Array.Empty<RecipeTransferMappingData>()).Select(transfer => $"{transfer.sourceInputId}:{transfer.targetOutputId}:{transfer.targetComponentId}:{transfer.quantityTransferPolicy}")),
                string.Join(",", (steps ?? Array.Empty<RecipeProcedureStepData>()).Select(step => $"{step.stepId}:{step.stepKind}:{string.Join("+", step.dependsOnStepIds ?? Array.Empty<string>())}")),
                string.Join(",", requirementIds ?? Array.Empty<string>())
            });
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string Redact(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : "redacted";
        }
    }
}
