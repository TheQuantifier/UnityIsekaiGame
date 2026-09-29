using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Recipes
{
    [Serializable]
    public sealed class RecipeCraftingScalingData
    {
        public float baseDurationSeconds = 10f;
        public float minimumDurationSeconds = 1f;
        public string[] eligibleSkillIds = Array.Empty<string>();
        public float unskilledDurationMultiplier = 1.25f;
        public float durationReductionPerSkillGrade = 0.06f;
        public float minimumSkillDurationMultiplier = 0.55f;
        public float rarityDurationMultiplierPerRank = 0.12f;
        public float statDurationMultiplierPerComplexityPoint = 0.03f;
        public float unskilledQualityAdjustment = -0.05f;
        public float qualityBonusPerSkillGrade = 0.04f;
        public float maximumSkillQualityBonus = 0.35f;
        public float affixChanceBonusPerSkillGrade = 0.025f;

        public RecipeCraftingScalingData Clone()
        {
            return new RecipeCraftingScalingData
            {
                baseDurationSeconds = Math.Max(0.1f, baseDurationSeconds),
                minimumDurationSeconds = Math.Max(0.1f, minimumDurationSeconds),
                eligibleSkillIds = ProductionStageDefinitionData.NormalizeIds(eligibleSkillIds),
                unskilledDurationMultiplier = Math.Max(0.1f, unskilledDurationMultiplier),
                durationReductionPerSkillGrade = Math.Max(0f, durationReductionPerSkillGrade),
                minimumSkillDurationMultiplier = Math.Max(0.1f, minimumSkillDurationMultiplier),
                rarityDurationMultiplierPerRank = Math.Max(0f, rarityDurationMultiplierPerRank),
                statDurationMultiplierPerComplexityPoint = Math.Max(0f, statDurationMultiplierPerComplexityPoint),
                unskilledQualityAdjustment = Math.Min(0f, unskilledQualityAdjustment),
                qualityBonusPerSkillGrade = Math.Max(0f, qualityBonusPerSkillGrade),
                maximumSkillQualityBonus = Math.Max(0f, maximumSkillQualityBonus),
                affixChanceBonusPerSkillGrade = Math.Max(0f, affixChanceBonusPerSkillGrade)
            };
        }
    }

    [Serializable]
    public sealed class RecipeBatchPolicyData
    {
        public RecipeBatchScalingPolicy scalingPolicy = RecipeBatchScalingPolicy.Fixed;
        public float baseBatchSize = 1f;
        public float minimumBatchSize = 1f;
        public float maximumBatchSize = 1f;
        public float batchIncrement = 1f;

        public RecipeBatchPolicyData Clone()
        {
            return new RecipeBatchPolicyData
            {
                scalingPolicy = scalingPolicy,
                baseBatchSize = baseBatchSize,
                minimumBatchSize = minimumBatchSize,
                maximumBatchSize = maximumBatchSize,
                batchIncrement = batchIncrement
            };
        }
    }

    [Serializable]
    public sealed class RecipeInputSpecificationData
    {
        public string inputId;
        public RecipeInputRole role = RecipeInputRole.Unknown;
        public RecipeRequirementState requirementState = RecipeRequirementState.Required;
        public RecipeInputClassification classification = RecipeInputClassification.Consumable;
        public string itemDefinitionId;
        public string materialDefinitionId;
        public string[] itemCategoryIds = Array.Empty<string>();
        public string[] materialTagIds = Array.Empty<string>();
        public string componentRoleId;
        public float quantity = 1f;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public float minimumQuality;
        public float minimumDurability;
        public float minimumPurity;
        public bool allowPartialStacks = true;
        public bool allowMultipleSources = true;
        public string accessPolicyId;
        public string[] substitutionIds = Array.Empty<string>();
        public RecipeTransferPolicy transferPolicy = RecipeTransferPolicy.InputDerived;
        public bool hidden;
        public bool selected;

        public RecipeInputSpecificationData CloneScaled(float factor)
        {
            return new RecipeInputSpecificationData
            {
                inputId = inputId ?? string.Empty,
                role = role,
                requirementState = requirementState,
                classification = classification,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                materialDefinitionId = materialDefinitionId ?? string.Empty,
                itemCategoryIds = CloneIds(itemCategoryIds),
                materialTagIds = CloneIds(materialTagIds),
                componentRoleId = componentRoleId ?? string.Empty,
                quantity = quantity * factor,
                unit = unit,
                minimumQuality = minimumQuality,
                minimumDurability = minimumDurability,
                minimumPurity = minimumPurity,
                allowPartialStacks = allowPartialStacks,
                allowMultipleSources = allowMultipleSources,
                accessPolicyId = accessPolicyId ?? string.Empty,
                substitutionIds = CloneIds(substitutionIds),
                transferPolicy = transferPolicy,
                hidden = hidden,
                selected = selected
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class RecipeOutputSpecificationData
    {
        public string outputId;
        public RecipeOutputRole role = RecipeOutputRole.Unknown;
        public string itemDefinitionId;
        public string materialDefinitionId;
        public string componentFoundationId;
        public float quantity = 1f;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public string compositionTemplateId;
        public RecipeTransferPolicy compositionTransferPolicy = RecipeTransferPolicy.PolicyDerived;
        public RecipeQualityPolicy qualityPolicy = RecipeQualityPolicy.PolicyReference;
        public string qualityPolicyId;
        public RecipeAffixPolicy affixPolicy = RecipeAffixPolicy.PolicyReference;
        public string affixPolicyId;
        public RecipeDurabilityPolicy durabilityPolicy = RecipeDurabilityPolicy.PolicyReference;
        public string durabilityPolicyId;
        public bool conditional;
        public bool failureOutput;

        public RecipeOutputSpecificationData CloneScaled(float factor)
        {
            return new RecipeOutputSpecificationData
            {
                outputId = outputId ?? string.Empty,
                role = role,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                materialDefinitionId = materialDefinitionId ?? string.Empty,
                componentFoundationId = componentFoundationId ?? string.Empty,
                quantity = quantity * factor,
                unit = unit,
                compositionTemplateId = compositionTemplateId ?? string.Empty,
                compositionTransferPolicy = compositionTransferPolicy,
                qualityPolicy = qualityPolicy,
                qualityPolicyId = qualityPolicyId ?? string.Empty,
                affixPolicy = affixPolicy,
                affixPolicyId = affixPolicyId ?? string.Empty,
                durabilityPolicy = durabilityPolicy,
                durabilityPolicyId = durabilityPolicyId ?? string.Empty,
                conditional = conditional,
                failureOutput = failureOutput
            };
        }
    }

    [Serializable]
    public sealed class RecipeTransferMappingData
    {
        public string mappingId;
        public string sourceInputId;
        public string targetOutputId;
        public string targetComponentId;
        public RecipeTransferPolicy quantityTransferPolicy = RecipeTransferPolicy.InputDerived;
        public float lossFraction;
        public bool preserveTrackedComponent;
        public bool transferProvenance = true;

        public RecipeTransferMappingData Clone()
        {
            return (RecipeTransferMappingData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class RecipeProcedureStepData
    {
        public string stepId;
        public RecipeProcedureStepKind stepKind = RecipeProcedureStepKind.Unknown;
        public string displayName;
        public string[] dependsOnStepIds = Array.Empty<string>();
        public bool optional;
        public bool conditional;
        public int repeatCount = 1;
        public string requiredKnowledgeFactId;
        public string requirementId;
        public bool hidden;

        public RecipeProcedureStepData Clone()
        {
            return new RecipeProcedureStepData
            {
                stepId = stepId ?? string.Empty,
                stepKind = stepKind,
                displayName = displayName ?? string.Empty,
                dependsOnStepIds = (dependsOnStepIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                optional = optional,
                conditional = conditional,
                repeatCount = Math.Max(1, repeatCount),
                requiredKnowledgeFactId = requiredKnowledgeFactId ?? string.Empty,
                requirementId = requirementId ?? string.Empty,
                hidden = hidden
            };
        }
    }

    [Serializable]
    public sealed class RecipeVersionData
    {
        public string versionId;
        public string versionLabel;
        public string priorVersionId;
        public string supersedingVersionId;
        public string worldTime;
        public string authorOrSourceId;
        public string changeReason;
        public RecipeLifecycleState state = RecipeLifecycleState.Active;
        public string accessPolicyId;

        public RecipeVersionData Clone()
        {
            return (RecipeVersionData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class RecipeVariantData
    {
        public string variantId;
        public string baseVersionId;
        public string variantGroupId;
        public string displayName;
        public string[] eligibilityKeys = Array.Empty<string>();
        public RecipeInputSpecificationData[] additionalInputs = Array.Empty<RecipeInputSpecificationData>();
        public RecipeOutputSpecificationData[] outputOverrides = Array.Empty<RecipeOutputSpecificationData>();
        public string[] additionalRequirementIds = Array.Empty<string>();
        public string accessPolicyId;
        public bool hidden;

        public RecipeVariantData Clone()
        {
            return new RecipeVariantData
            {
                variantId = variantId ?? string.Empty,
                baseVersionId = baseVersionId ?? string.Empty,
                variantGroupId = variantGroupId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                eligibilityKeys = (eligibilityKeys ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                additionalInputs = (additionalInputs ?? Array.Empty<RecipeInputSpecificationData>()).Select(input => input?.CloneScaled(1f)).Where(input => input != null).ToArray(),
                outputOverrides = (outputOverrides ?? Array.Empty<RecipeOutputSpecificationData>()).Select(output => output?.CloneScaled(1f)).Where(output => output != null).ToArray(),
                additionalRequirementIds = (additionalRequirementIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                accessPolicyId = accessPolicyId ?? string.Empty,
                hidden = hidden
            };
        }
    }

    public sealed class RecipeResolvedSnapshot
    {
        public RecipeResolvedSnapshot(string recipeId, string versionId, string variantId, float batchSize, IReadOnlyList<RecipeInputSpecificationData> inputs, IReadOnlyList<RecipeOutputSpecificationData> outputs, IReadOnlyList<RecipeTransferMappingData> transfers, IReadOnlyList<RecipeProcedureStepData> procedureSteps, IReadOnlyList<string> requirementIds, bool redacted, string signature)
        {
            RecipeId = recipeId ?? string.Empty;
            VersionId = versionId ?? string.Empty;
            VariantId = variantId ?? string.Empty;
            BatchSize = batchSize;
            Inputs = (inputs ?? Array.Empty<RecipeInputSpecificationData>()).Select(input => input.CloneScaled(1f)).ToArray();
            Outputs = (outputs ?? Array.Empty<RecipeOutputSpecificationData>()).Select(output => output.CloneScaled(1f)).ToArray();
            TransferMappings = (transfers ?? Array.Empty<RecipeTransferMappingData>()).Select(mapping => mapping.Clone()).ToArray();
            ProcedureSteps = (procedureSteps ?? Array.Empty<RecipeProcedureStepData>()).Select(step => step.Clone()).ToArray();
            RequirementIds = (requirementIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Redacted = redacted;
            Signature = signature ?? string.Empty;
        }

        public string RecipeId { get; }
        public string VersionId { get; }
        public string VariantId { get; }
        public float BatchSize { get; }
        public IReadOnlyList<RecipeInputSpecificationData> Inputs { get; }
        public IReadOnlyList<RecipeOutputSpecificationData> Outputs { get; }
        public IReadOnlyList<RecipeTransferMappingData> TransferMappings { get; }
        public IReadOnlyList<RecipeProcedureStepData> ProcedureSteps { get; }
        public IReadOnlyList<string> RequirementIds { get; }
        public bool Redacted { get; }
        public string Signature { get; }

        public RecipeResolvedSnapshot Clone()
        {
            return new RecipeResolvedSnapshot(RecipeId, VersionId, VariantId, BatchSize, Inputs, Outputs, TransferMappings, ProcedureSteps, RequirementIds, Redacted, Signature);
        }

        public InformationSubjectReferenceData CreateInformationSubject(string ownerPersonId = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = string.IsNullOrWhiteSpace(VersionId) ? RecipeId : VersionId,
                parentSubjectId = RecipeId,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = new[] { "domain.item", "recipe", "production.recipe" }
            };
        }
    }
}
