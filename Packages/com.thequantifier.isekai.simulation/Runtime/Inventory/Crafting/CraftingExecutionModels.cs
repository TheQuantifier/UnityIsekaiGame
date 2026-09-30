using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Crafting
{
    [Serializable]
    public sealed class CraftingExecutionRequest
    {
        public string operationId;
        public string recipeId;
        public string versionId;
        public string variantId;
        public float batchSize = 1f;
        public string[] selectedOptionalInputIds = Array.Empty<string>();
        public string actorPersonId;
        public string actorBodyId;
        public string ownerPersonId;
        public string custodianPersonId;
        public string locationId;
        public string worldTime;
        public string deterministicSeed;
        public string craftingSkillId;
        public int craftingSkillGrade;
        public bool craftingSkillUsed;
        public float craftDurationSeconds;
        public float craftingQualityAdjustment;
        public float craftingAffixChanceBonus;
        public List<CraftingCatalystUseData> catalysts = new List<CraftingCatalystUseData>();
        public ProductionContextData productionContext = new ProductionContextData();
        public CraftingFailurePolicy failurePolicy = CraftingFailurePolicy.FullRollback;
        public RecipeProjectionAccessLevel accessLevel = RecipeProjectionAccessLevel.Privileged;
        public bool personFacing;
        public bool preview;

        public CraftingExecutionRequest Clone()
        {
            return new CraftingExecutionRequest
            {
                operationId = operationId ?? string.Empty,
                recipeId = recipeId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                batchSize = batchSize,
                selectedOptionalInputIds = CloneIds(selectedOptionalInputIds),
                actorPersonId = actorPersonId ?? string.Empty,
                actorBodyId = actorBodyId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                custodianPersonId = custodianPersonId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                craftingSkillId = craftingSkillId ?? string.Empty,
                craftingSkillGrade = craftingSkillGrade,
                craftingSkillUsed = craftingSkillUsed,
                craftDurationSeconds = Math.Max(0f, craftDurationSeconds),
                craftingQualityAdjustment = craftingQualityAdjustment,
                craftingAffixChanceBonus = Math.Max(0f, craftingAffixChanceBonus),
                catalysts = catalysts == null ? new List<CraftingCatalystUseData>() : catalysts.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                productionContext = productionContext?.Clone() ?? new ProductionContextData(),
                failurePolicy = failurePolicy,
                accessLevel = accessLevel,
                personFacing = personFacing,
                preview = preview
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class CraftingConsumedInputData
    {
        public string allocationId;
        public string inputId;
        public string itemInstanceId;
        public string definitionId;
        public float quantity;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public bool reusable;
        public bool consumed;

        public CraftingConsumedInputData Clone()
        {
            return new CraftingConsumedInputData
            {
                allocationId = allocationId ?? string.Empty,
                inputId = inputId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                definitionId = definitionId ?? string.Empty,
                quantity = quantity,
                unit = unit,
                reusable = reusable,
                consumed = consumed
            };
        }
    }

    [Serializable]
    public sealed class CraftingToolUseData
    {
        public string requirementId;
        public string toolItemInstanceId;
        public string toolDefinitionId;
        public float wearApplied;
        public bool applied;

        public CraftingToolUseData Clone()
        {
            return new CraftingToolUseData
            {
                requirementId = requirementId ?? string.Empty,
                toolItemInstanceId = toolItemInstanceId ?? string.Empty,
                toolDefinitionId = toolDefinitionId ?? string.Empty,
                wearApplied = wearApplied,
                applied = applied
            };
        }
    }

    [Serializable]
    public sealed class CraftingOutputItemData
    {
        public string outputId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public string materialDefinitionId;
        public CraftingOutputKind outputKind = CraftingOutputKind.Primary;
        public float quantity = 1f;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public bool createdItemInstance;

        public CraftingOutputItemData Clone()
        {
            return new CraftingOutputItemData
            {
                outputId = outputId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                materialDefinitionId = materialDefinitionId ?? string.Empty,
                outputKind = outputKind,
                quantity = quantity,
                unit = unit,
                createdItemInstance = createdItemInstance
            };
        }
    }

    [Serializable]
    public sealed class CraftingOperationRecordData
    {
        public string operationId;
        public string recipeId;
        public string versionId;
        public string variantId;
        public string actorPersonId;
        public string actorBodyId;
        public string ownerPersonId;
        public string locationId;
        public string worldTime;
        public string deterministicSeed;
        public string craftingSkillId;
        public int craftingSkillGrade;
        public bool craftingSkillUsed;
        public float craftDurationSeconds;
        public float craftingQualityAdjustment;
        public float craftingAffixChanceBonus;
        public List<CraftingCatalystUseData> catalysts = new List<CraftingCatalystUseData>();
        public string recipeSignature;
        public string requirementPlanId;
        public CraftingOperationState state = CraftingOperationState.Prepared;
        public CraftingExecutionStatus status = CraftingExecutionStatus.Preview;
        public CraftingFailurePolicy failurePolicy = CraftingFailurePolicy.FullRollback;
        public List<CraftingConsumedInputData> consumedInputs = new List<CraftingConsumedInputData>();
        public List<CraftingToolUseData> toolUses = new List<CraftingToolUseData>();
        public List<CraftingOutputItemData> outputs = new List<CraftingOutputItemData>();
        public string[] diagnostics = Array.Empty<string>();
        public long revision = 1L;

        public CraftingOperationRecordData Clone()
        {
            return new CraftingOperationRecordData
            {
                operationId = operationId ?? string.Empty,
                recipeId = recipeId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                actorBodyId = actorBodyId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                craftingSkillId = craftingSkillId ?? string.Empty,
                craftingSkillGrade = craftingSkillGrade,
                craftingSkillUsed = craftingSkillUsed,
                craftDurationSeconds = Math.Max(0f, craftDurationSeconds),
                craftingQualityAdjustment = craftingQualityAdjustment,
                craftingAffixChanceBonus = Math.Max(0f, craftingAffixChanceBonus),
                catalysts = catalysts == null ? new List<CraftingCatalystUseData>() : catalysts.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                recipeSignature = recipeSignature ?? string.Empty,
                requirementPlanId = requirementPlanId ?? string.Empty,
                state = state,
                status = status,
                failurePolicy = failurePolicy,
                consumedInputs = consumedInputs == null ? new List<CraftingConsumedInputData>() : consumedInputs.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                toolUses = toolUses == null ? new List<CraftingToolUseData>() : toolUses.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                outputs = outputs == null ? new List<CraftingOutputItemData>() : outputs.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                diagnostics = CloneIds(diagnostics),
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = operationId ?? string.Empty,
                parentSubjectId = recipeId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = new[] { "domain.item", "item.crafting", "crafting.operation" }
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Serializable]
    public sealed class CraftingExecutionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<CraftingOperationRecordData> operations = new List<CraftingOperationRecordData>();

        public CraftingExecutionRuntimeSaveData Clone()
        {
            return new CraftingExecutionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                operations = operations == null ? new List<CraftingOperationRecordData>() : operations.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList()
            };
        }
    }

    public sealed class CraftingExecutionResult
    {
        private CraftingExecutionResult(bool succeeded, bool preview, bool duplicate, CraftingExecutionStatus status, string message, CraftingOperationRecordData operation, RecipeResolutionResult recipeResult)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            Operation = operation?.Clone();
            RecipeResult = recipeResult;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public CraftingExecutionStatus Status { get; }
        public string Message { get; }
        public CraftingOperationRecordData Operation { get; }
        public RecipeResolutionResult RecipeResult { get; }

        public static CraftingExecutionResult Success(CraftingOperationRecordData operation, string message, bool preview = false, bool duplicate = false, RecipeResolutionResult recipeResult = null)
        {
            CraftingExecutionStatus status = duplicate ? CraftingExecutionStatus.Duplicate : preview ? CraftingExecutionStatus.Preview : CraftingExecutionStatus.Succeeded;
            return new CraftingExecutionResult(true, preview, duplicate, status, message, operation, recipeResult);
        }

        public static CraftingExecutionResult Failure(CraftingExecutionStatus status, string message, CraftingOperationRecordData operation = null, RecipeResolutionResult recipeResult = null)
        {
            return new CraftingExecutionResult(false, false, false, status, message, operation, recipeResult);
        }
    }
}
