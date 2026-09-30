using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Combat.Defense
{
    [CreateAssetMenu(fileName = "DefensiveActionDefinition", menuName = "Unity Isekai Game/Combat/Defensive Action")]
    public sealed class DefensiveActionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string defensiveActionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private DefensiveActionType actionType = DefensiveActionType.Guard;
        [SerializeField, Min(0f)] private float activationStaminaCost;
        [SerializeField, Min(0f)] private float successStaminaCost;
        [SerializeField, Min(0f)] private float failureStaminaCost;
        [SerializeField, Range(0f, 1f)] private float baseChance = 1f;
        [SerializeField, Range(0f, 1f)] private float minimumChance;
        [SerializeField, Range(0f, 1f)] private float maximumChance = 1f;
        [SerializeField] private string contributingStatId;
        [SerializeField] private float statContributionScale;
        [SerializeField] private string contributingSkillId;
        [SerializeField] private float skillContributionScale;
        [SerializeField] private bool requiresEquipmentSource;
        [SerializeField] private string requiredEquipmentCategoryId;
        [SerializeField] private string requiredEquipmentTagId;
        [SerializeField, Min(0f)] private float timingWindowSeconds;
        [SerializeField] private bool maintained;
        [SerializeField] private bool consumedAfterAttempt;
        [SerializeField] private DefensiveDamageReductionMode reductionMode = DefensiveDamageReductionMode.None;
        [SerializeField, Min(0f)] private float flatReduction;
        [SerializeField, Range(0f, 1f)] private float percentageReduction;
        [SerializeField, Min(0f)] private float fullPreventionThreshold;
        [SerializeField] private bool allowsTrueDamageDefense;
        [SerializeField] private bool allowsCriticalDefense = true;
        [SerializeField] private DamageTypeDefinition[] applicableDamageTypes;
        [SerializeField, TextArea(1, 3)] private string notes;

        public string DefensiveActionId => defensiveActionId;
        public string Id => defensiveActionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public DefensiveActionType ActionType => actionType;
        public float ActivationStaminaCost => Mathf.Max(0f, activationStaminaCost);
        public float SuccessStaminaCost => Mathf.Max(0f, successStaminaCost);
        public float FailureStaminaCost => Mathf.Max(0f, failureStaminaCost);
        public float BaseChance => Mathf.Clamp01(baseChance);
        public float MinimumChance => Mathf.Clamp01(minimumChance);
        public float MaximumChance => Mathf.Clamp01(maximumChance);
        public string ContributingStatId => contributingStatId ?? string.Empty;
        public float StatContributionScale => statContributionScale;
        public string ContributingSkillId => contributingSkillId ?? string.Empty;
        public float SkillContributionScale => skillContributionScale;
        public bool RequiresEquipmentSource => requiresEquipmentSource;
        public string RequiredEquipmentCategoryId => requiredEquipmentCategoryId ?? string.Empty;
        public string RequiredEquipmentTagId => requiredEquipmentTagId ?? string.Empty;
        public float TimingWindowSeconds => Mathf.Max(0f, timingWindowSeconds);
        public bool Maintained => maintained;
        public bool ConsumedAfterAttempt => consumedAfterAttempt;
        public DefensiveDamageReductionMode ReductionMode => reductionMode;
        public float FlatReduction => Mathf.Max(0f, flatReduction);
        public float PercentageReduction => Mathf.Clamp01(percentageReduction);
        public float FullPreventionThreshold => Mathf.Max(0f, fullPreventionThreshold);
        public bool AllowsTrueDamageDefense => allowsTrueDamageDefense;
        public bool AllowsCriticalDefense => allowsCriticalDefense;
        public IReadOnlyList<DamageTypeDefinition> ApplicableDamageTypes => applicableDamageTypes ?? System.Array.Empty<DamageTypeDefinition>();
        public string Notes => notes;

        public bool IsTimedWindow => TimingWindowSeconds > 0f && !Maintained;
        public DefensiveActionState RuntimeState => actionType switch
        {
            DefensiveActionType.Guard => DefensiveActionState.Guarding,
            DefensiveActionType.Block => DefensiveActionState.BlockWindow,
            DefensiveActionType.Parry => DefensiveActionState.ParryWindow,
            DefensiveActionType.Dodge => DefensiveActionState.DodgeWindow,
            _ => DefensiveActionState.Inactive
        };

        private void OnValidate()
        {
            defensiveActionId = defensiveActionId?.Trim();
            contributingStatId = contributingStatId?.Trim();
            contributingSkillId = contributingSkillId?.Trim();
            requiredEquipmentCategoryId = requiredEquipmentCategoryId?.Trim();
            requiredEquipmentTagId = requiredEquipmentTagId?.Trim();
            activationStaminaCost = Mathf.Max(0f, activationStaminaCost);
            successStaminaCost = Mathf.Max(0f, successStaminaCost);
            failureStaminaCost = Mathf.Max(0f, failureStaminaCost);
            minimumChance = Mathf.Clamp01(minimumChance);
            maximumChance = Mathf.Clamp01(maximumChance);
            if (maximumChance < minimumChance)
            {
                maximumChance = minimumChance;
            }

            baseChance = Mathf.Clamp(baseChance, minimumChance, maximumChance);
            timingWindowSeconds = Mathf.Max(0f, timingWindowSeconds);
            flatReduction = Mathf.Max(0f, flatReduction);
            percentageReduction = Mathf.Clamp01(percentageReduction);
            fullPreventionThreshold = Mathf.Max(0f, fullPreventionThreshold);
        }

        public bool AppliesToDamageType(DamageTypeDefinition damageType)
        {
            if (applicableDamageTypes == null || applicableDamageTypes.Length == 0 || damageType == null)
            {
                return true;
            }

            for (int i = 0; i < applicableDamageTypes.Length; i++)
            {
                DamageTypeDefinition applicable = applicableDamageTypes[i];
                if (applicable != null && damageType.IsOrInheritsFrom(applicable))
                {
                    return true;
                }
            }

            return false;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("defense-action.", System.StringComparison.Ordinal))
            {
                report.AddWarning($"DefensiveActionDefinition '{DisplayName}' should use the 'defense-action.' namespace prefix.");
            }

            if (actionType == DefensiveActionType.None)
            {
                report.AddError($"DefensiveActionDefinition '{DisplayName}' must choose a defensive action type.");
            }

            if (MaximumChance < MinimumChance)
            {
                report.AddError($"DefensiveActionDefinition '{Id}' maximum chance cannot be below minimum chance.");
            }

            if (!IsFinite(BaseChance) || !IsFinite(MinimumChance) || !IsFinite(MaximumChance))
            {
                report.AddError($"DefensiveActionDefinition '{Id}' chance values must be finite.");
            }

            if (!IsFinite(ActivationStaminaCost) || !IsFinite(SuccessStaminaCost) || !IsFinite(FailureStaminaCost))
            {
                report.AddError($"DefensiveActionDefinition '{Id}' stamina costs must be finite.");
            }

            if (!IsFinite(TimingWindowSeconds))
            {
                report.AddError($"DefensiveActionDefinition '{Id}' timing window must be finite.");
            }

            if (actionType == DefensiveActionType.Guard && !maintained)
            {
                report.AddWarning($"DefensiveActionDefinition '{Id}' is Guard but is not marked maintained.");
            }

            if ((actionType == DefensiveActionType.Dodge || actionType == DefensiveActionType.Parry) && !consumedAfterAttempt)
            {
                report.AddWarning($"DefensiveActionDefinition '{Id}' is {actionType} but is not consumed after a valid attempt.");
            }

            ValidateDamageReferences(definitionsById, report);
            ValidateOptionalDefinitionReference<CategoryDefinition>(requiredEquipmentCategoryId, "required equipment category", definitionsById, report);
            ValidateOptionalDefinitionReference<TagDefinition>(requiredEquipmentTagId, "required equipment tag", definitionsById, report);
            ValidateOptionalDefinitionReference<UnityIsekaiGame.Skills.SkillDefinition>(contributingSkillId, "contributing skill", definitionsById, report);
        }

        private void ValidateDamageReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (applicableDamageTypes == null)
            {
                return;
            }

            for (int i = 0; i < applicableDamageTypes.Length; i++)
            {
                DamageTypeDefinition damageType = applicableDamageTypes[i];
                if (damageType == null)
                {
                    continue;
                }

                if (definitionsById == null
                    || !definitionsById.TryGetValue(damageType.Id, out IGameDefinition found)
                    || found is not DamageTypeDefinition)
                {
                    report.AddError($"DefensiveActionDefinition '{Id}' references DamageType '{damageType.Id}', which is not in the configured catalog.");
                }
            }
        }

        private void ValidateOptionalDefinitionReference<TDefinition>(string definitionId, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(definitionId, out IGameDefinition found)
                || found is not TDefinition)
            {
                report.AddError($"DefensiveActionDefinition '{Id}' {label} '{definitionId}' is not in the configured catalog.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
