using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Execution
{
    [CreateAssetMenu(fileName = "CombatExecutionDefinition", menuName = "Unity Isekai Game/Combat/Combat Execution")]
    public sealed class CombatExecutionDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string executionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private CombatExecutionActionType actionType = CombatExecutionActionType.Attack;
        [SerializeField, Min(0f)] private float windUpDuration;
        [SerializeField, Min(0f)] private float recoveryDuration;
        [SerializeField, Min(0f)] private float cooldownDuration;
        [SerializeField] private CombatExecutionCooldownStartPoint cooldownStartPoint = CombatExecutionCooldownStartPoint.OnExecution;
        [SerializeField] private CombatExecutionCooldownScope cooldownScope = CombatExecutionCooldownScope.Definition;
        [SerializeField] private string cooldownGroupKey;
        [SerializeField] private string globalCooldownGroupKey = "combat.global";
        [SerializeField, Min(1)] private int maximumCharges = 1;
        [SerializeField, Min(0f)] private float chargeRecoveryDuration;
        [SerializeField] private bool sequentialChargeRecovery = true;
        [SerializeField] private CombatExecutionCostCommitPoint defaultCostCommitPoint = CombatExecutionCostCommitPoint.OnExecution;
        [SerializeField] private CombatExecutionRefundPolicy beginCostRefundPolicy = CombatExecutionRefundPolicy.RefundIfCancelledBeforeExecution;
        [SerializeField] private CombatExecutionInterruptionPolicy interruptionPolicy = CombatExecutionInterruptionPolicy.InterruptBeforeExecution;
        [SerializeField] private CombatCommitmentCategory commitmentCategory = CombatCommitmentCategory.Attack;
        [SerializeField] private CombatCommitmentCategory[] allowedOverlaps;
        [SerializeField] private bool movementAllowed;
        [SerializeField] private bool defensiveActionsAllowed = true;
        [SerializeField] private bool anotherAttackAllowed;
        [SerializeField] private bool itemUseAllowed;
        [SerializeField] private bool ordinaryAbilitiesAllowed;
        [SerializeField] private RequirementSetDefinition requirements;
        [SerializeField] private CombatExecutionCostDefinition[] costs;

        public string Id => executionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.General;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public CombatExecutionActionType ActionType => actionType;
        public float WindUpDuration => Mathf.Max(0f, windUpDuration);
        public float RecoveryDuration => Mathf.Max(0f, recoveryDuration);
        public float CooldownDuration => Mathf.Max(0f, cooldownDuration);
        public CombatExecutionCooldownStartPoint CooldownStartPoint => cooldownStartPoint;
        public CombatExecutionCooldownScope CooldownScope => cooldownScope;
        public string CooldownGroupKey => cooldownGroupKey ?? string.Empty;
        public string GlobalCooldownGroupKey => string.IsNullOrWhiteSpace(globalCooldownGroupKey) ? "combat.global" : globalCooldownGroupKey;
        public int MaximumCharges => Mathf.Max(1, maximumCharges);
        public float ChargeRecoveryDuration => Mathf.Max(0f, chargeRecoveryDuration);
        public bool SequentialChargeRecovery => sequentialChargeRecovery;
        public CombatExecutionCostCommitPoint DefaultCostCommitPoint => defaultCostCommitPoint;
        public CombatExecutionRefundPolicy BeginCostRefundPolicy => beginCostRefundPolicy;
        public CombatExecutionInterruptionPolicy InterruptionPolicy => interruptionPolicy;
        public CombatCommitmentCategory CommitmentCategory => commitmentCategory;
        public IReadOnlyList<CombatCommitmentCategory> AllowedOverlaps => allowedOverlaps ?? Array.Empty<CombatCommitmentCategory>();
        public bool MovementAllowed => movementAllowed;
        public bool DefensiveActionsAllowed => defensiveActionsAllowed;
        public bool AnotherAttackAllowed => anotherAttackAllowed;
        public bool ItemUseAllowed => itemUseAllowed;
        public bool OrdinaryAbilitiesAllowed => ordinaryAbilitiesAllowed;
        public RequirementSetDefinition Requirements => requirements;
        public IReadOnlyList<CombatExecutionCostDefinition> Costs => costs ?? Array.Empty<CombatExecutionCostDefinition>();

        public string ResolveCooldownKey()
        {
            return cooldownScope switch
            {
                CombatExecutionCooldownScope.CooldownGroup => string.IsNullOrWhiteSpace(CooldownGroupKey) ? Id : CooldownGroupKey,
                CombatExecutionCooldownScope.GlobalGroup => GlobalCooldownGroupKey,
                _ => Id
            };
        }

        public bool CanOverlapWith(CombatCommitmentCategory other)
        {
            if (commitmentCategory == CombatCommitmentCategory.None || other == CombatCommitmentCategory.None)
            {
                return true;
            }

            IReadOnlyList<CombatCommitmentCategory> overlaps = AllowedOverlaps;
            for (int i = 0; i < overlaps.Count; i++)
            {
                if (overlaps[i] == other)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            executionId = executionId?.Trim();
            cooldownGroupKey = cooldownGroupKey?.Trim();
            globalCooldownGroupKey = globalCooldownGroupKey?.Trim();
            windUpDuration = Mathf.Max(0f, windUpDuration);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
            maximumCharges = Mathf.Max(1, maximumCharges);
            chargeRecoveryDuration = Mathf.Max(0f, chargeRecoveryDuration);

            if (costs == null)
            {
                return;
            }

            for (int i = 0; i < costs.Length; i++)
            {
                costs[i]?.Validate();
            }
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"CombatExecutionDefinition '{name}' is missing a stable ID.");
            }
            else
            {
                report.AddRange(DefinitionIdValidator.Validate(Id, $"CombatExecutionDefinition '{DisplayName}' ID").ToReport());
                if (!Id.StartsWith("combat-execution.", StringComparison.Ordinal))
                {
                    report.AddWarning($"CombatExecutionDefinition '{Id}' should use the 'combat-execution.' namespace prefix.");
                }
            }

            if (actionType == CombatExecutionActionType.None)
            {
                report.AddError($"CombatExecutionDefinition '{DisplayName}' must choose an action type.");
            }

            ValidateFinite(WindUpDuration, "wind-up duration", report);
            ValidateFinite(RecoveryDuration, "recovery duration", report);
            ValidateFinite(CooldownDuration, "cooldown duration", report);
            ValidateFinite(ChargeRecoveryDuration, "charge recovery duration", report);

            if (MaximumCharges > 1 && ChargeRecoveryDuration <= 0f && CooldownDuration <= 0f)
            {
                report.AddError($"CombatExecutionDefinition '{Id}' grants multiple charges but has no cooldown or charge recovery duration.");
            }

            ValidateReference<CategoryDefinition>(primaryCategory == null ? string.Empty : primaryCategory.Id, "primary category", definitionsById, report, optional: true);
            ValidateReference<RequirementSetDefinition>(requirements == null ? string.Empty : requirements.Id, "requirement set", definitionsById, report, optional: true);

            IReadOnlyList<TagDefinition> tagList = Tags;
            for (int i = 0; i < tagList.Count; i++)
            {
                ValidateReference<TagDefinition>(tagList[i] == null ? string.Empty : tagList[i].Id, "tag", definitionsById, report, optional: true);
            }

            IReadOnlyList<CombatExecutionCostDefinition> costList = Costs;
            for (int i = 0; i < costList.Count; i++)
            {
                CombatExecutionCostDefinition cost = costList[i];
                if (cost == null)
                {
                    report.AddError($"CombatExecutionDefinition '{Id}' has a missing cost entry at index {i}.");
                    continue;
                }

                cost.ValidateCatalogDefinition(Id, i, definitionsById, report);
            }
        }

        private void ValidateFinite(float value, string label, DefinitionValidationReport report)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                report.AddError($"CombatExecutionDefinition '{Id}' {label} must be finite.");
            }
        }

        private void ValidateReference<TDefinition>(string definitionId, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, bool optional)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                if (!optional)
                {
                    report.AddError($"CombatExecutionDefinition '{Id}' is missing {label}.");
                }

                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(definitionId, out IGameDefinition found) || found is not TDefinition)
            {
                report.AddError($"CombatExecutionDefinition '{Id}' {label} '{definitionId}' is not in the configured catalog.");
            }
        }
    }

    [Serializable]
    public sealed class CombatExecutionCostDefinition
    {
        [SerializeField] private CombatExecutionCostType costType = CombatExecutionCostType.Resource;
        [SerializeField] private ResourceDefinition resource;
        [SerializeField] private ItemDefinition item;
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField, Min(0f)] private float amount;
        [SerializeField] private bool required = true;
        [SerializeField] private bool consumed = true;
        [SerializeField] private CombatExecutionCostCommitPoint commitPoint = CombatExecutionCostCommitPoint.OnExecution;
        [SerializeField] private CombatExecutionRefundPolicy refundPolicy = CombatExecutionRefundPolicy.RefundIfUnderlyingExecutionFails;
        [SerializeField, Min(0f)] private float minimumRemaining;

        public CombatExecutionCostType CostType => costType;
        public ResourceDefinition Resource => resource;
        public ItemDefinition Item => item;
        public CurrencyDefinition Currency => currency;
        public float Amount => Mathf.Max(0f, amount);
        public bool Required => required;
        public bool Consumed => consumed;
        public CombatExecutionCostCommitPoint CommitPoint => commitPoint;
        public CombatExecutionRefundPolicy RefundPolicy => refundPolicy;
        public float MinimumRemaining => Mathf.Max(0f, minimumRemaining);
        public string DefinitionId => costType switch
        {
            CombatExecutionCostType.Resource => resource == null ? string.Empty : resource.Id,
            CombatExecutionCostType.InventoryItem or CombatExecutionCostType.Ammunition => item == null ? string.Empty : item.Id,
            CombatExecutionCostType.Currency => currency == null ? string.Empty : currency.Id,
            _ => string.Empty
        };

        public void Validate()
        {
            amount = Mathf.Max(0f, amount);
            minimumRemaining = Mathf.Max(0f, minimumRemaining);
        }

        public void ValidateCatalogDefinition(string ownerId, int index, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (amount <= 0f && consumed)
            {
                report.AddError($"CombatExecutionDefinition '{ownerId}' cost {index} must have a positive amount when consumed.");
            }

            if (float.IsNaN(amount) || float.IsInfinity(amount) || float.IsNaN(minimumRemaining) || float.IsInfinity(minimumRemaining))
            {
                report.AddError($"CombatExecutionDefinition '{ownerId}' cost {index} numeric values must be finite.");
            }

            switch (costType)
            {
                case CombatExecutionCostType.Resource:
                    ValidateReference<ResourceDefinition>(ownerId, index, DefinitionId, "resource", definitionsById, report);
                    break;
                case CombatExecutionCostType.InventoryItem:
                case CombatExecutionCostType.Ammunition:
                    ValidateReference<ItemDefinition>(ownerId, index, DefinitionId, "item", definitionsById, report);
                    break;
                case CombatExecutionCostType.Currency:
                    ValidateReference<CurrencyDefinition>(ownerId, index, DefinitionId, "currency", definitionsById, report);
                    break;
            }
        }

        private static void ValidateReference<TDefinition>(string ownerId, int index, string definitionId, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                report.AddError($"CombatExecutionDefinition '{ownerId}' cost {index} is missing a {label} reference.");
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(definitionId, out IGameDefinition found) || found is not TDefinition)
            {
                report.AddError($"CombatExecutionDefinition '{ownerId}' cost {index} references {label} '{definitionId}', which is not in the configured catalog.");
            }
        }
    }

    internal static class DefinitionIdValidationResultExtensions
    {
        public static DefinitionValidationReport ToReport(this DefinitionIdValidationResult result)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            if (result == null)
            {
                return report;
            }

            foreach (DefinitionIdValidationMessage message in result.Messages)
            {
                report.Add(message.Severity, message.Message);
            }

            return report;
        }
    }
}
