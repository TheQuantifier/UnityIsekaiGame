using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Requirements
{
    public static class CapabilityRequirementEvaluator
    {
        private const float EqualityTolerance = 0.0001f;

        public static RequirementEvaluationResult Evaluate(RequirementSetDefinition requirementSet, RequirementEvaluationContext context)
        {
            RequirementEvaluationResult result = new RequirementEvaluationResult
            {
                RequirementSetId = requirementSet == null ? string.Empty : requirementSet.Id
            };

            if (requirementSet == null || requirementSet.RootGroup == null)
            {
                result.Passed = true;
                return result;
            }

            result.Passed = EvaluateGroup(requirementSet.RootGroup, context ?? new RequirementEvaluationContext(), result);
            return result;
        }

        private static bool EvaluateGroup(RequirementLogicalGroupDefinition group, RequirementEvaluationContext context, RequirementEvaluationResult result)
        {
            List<bool> childResults = new List<bool>();
            foreach (RequirementNodeDefinition node in group.Nodes)
            {
                RequirementNodeResult nodeResult = EvaluateNode(node, context);
                result.NodeResults.Add(nodeResult);
                childResults.Add(nodeResult.Passed);
            }

            foreach (RequirementLogicalGroupDefinition child in group.Groups)
            {
                childResults.Add(EvaluateGroup(child, context, result));
            }

            return group.LogicalOperator switch
            {
                RequirementLogicalOperator.Any => childResults.Any(value => value),
                RequirementLogicalOperator.None => childResults.All(value => !value),
                RequirementLogicalOperator.All or _ => childResults.All(value => value)
            };
        }

        private static RequirementNodeResult EvaluateNode(RequirementNodeDefinition node, RequirementEvaluationContext context)
        {
            bool passed;
            string internalReason = string.Empty;
            if (node == null)
            {
                return new RequirementNodeResult { Passed = false, InternalReason = "Missing requirement node.", PlayerFacingReason = "Requirement not met." };
            }

            if (node.RequiresContext && (context.ContextIds == null || !context.ContextIds.ContainsKey(node.ContextKey)))
            {
                passed = false;
                internalReason = $"Missing context '{node.ContextKey}'.";
            }
            else
            {
                passed = EvaluateNodeValue(node, context, out internalReason);
            }

            return new RequirementNodeResult
            {
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                Passed = passed,
                FailureVisibility = node.FailureVisibility,
                InternalReason = passed ? string.Empty : internalReason,
                PlayerFacingReason = passed ? string.Empty : node.FailureVisibility == RequirementFailureVisibility.Obscured ? node.ObscuredFailureReason : node.FailureVisibility == RequirementFailureVisibility.Hidden ? string.Empty : node.VisibleFailureReason
            };
        }

        private static bool EvaluateNodeValue(RequirementNodeDefinition node, RequirementEvaluationContext context, out string reason)
        {
            reason = string.Empty;
            switch (node.NodeType)
            {
                case RequirementNodeType.BaseAttribute:
                    return Compare(context.Attributes == null ? 0f : context.Attributes.GetValue(node.TargetId), node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.CalculatedStat:
                    return Compare(context.CalculatedStats == null ? 0f : context.CalculatedStats.GetValue(node.TargetId), node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.ResourceCurrent:
                    return Compare(context.Resources == null ? 0f : context.Resources.GetCurrent(node.TargetId), node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.ResourceMaximum:
                    return Compare(context.Resources == null ? 0f : context.Resources.GetMaximum(node.TargetId), node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.ResourceNormalized:
                    return Compare(context.Resources == null ? 0f : context.Resources.GetNormalized(node.TargetId), node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.SkillGrade:
                    return Compare((int)(context.Skills == null ? SkillGrade.F : context.Skills.GetGrade(node.TargetId)), node.IntegerValue, node.Comparison, out reason);
                case RequirementNodeType.TraitLifecycle:
                    TraitLifecycleState lifecycle = (TraitLifecycleState)node.IntegerValue;
                    bool has = context.Traits != null && context.Traits.TryGetTrait(node.TargetId, out RuntimeTraitRecord trait) && (TraitLifecycleState)trait.lifecycleState == lifecycle;
                    reason = has ? string.Empty : $"Trait '{node.TargetId}' is not {lifecycle}.";
                    return has == node.BooleanValue;
                case RequirementNodeType.Role:
                    bool role = context.Identity != null && context.Identity.Roles.Any(value => value.roleDefinitionId == node.TargetId && value.lifecycleState == RoleLifecycleState.Active);
                    reason = role ? string.Empty : $"Active Role '{node.TargetId}' is missing.";
                    return role == node.BooleanValue;
                case RequirementNodeType.SocialStatus:
                    bool social = context.Identity != null && context.Identity.SocialStatuses.Any(value => value.socialStatusDefinitionId == node.TargetId && value.lifecycleState == SocialStatusLifecycleState.Active && ContextMatches(node, context, value));
                    reason = social ? string.Empty : $"Social Status '{node.TargetId}' is missing in context.";
                    return social == node.BooleanValue;
                case RequirementNodeType.Origin:
                    bool origin = context.Identity != null && context.Identity.Origin != null && context.Identity.Origin.assigned && string.Equals(context.Identity.Origin.originId, node.TargetId, StringComparison.Ordinal);
                    reason = origin ? string.Empty : $"Origin '{node.TargetId}' is missing.";
                    return origin == node.BooleanValue;
                case RequirementNodeType.BirthGift:
                    bool gift = context.Identity != null && context.Identity.BirthGift != null && string.Equals(context.Identity.BirthGift.giftDefinitionId, node.TargetId, StringComparison.Ordinal);
                    reason = gift ? string.Empty : $"Birth gift '{node.TargetId}' is missing.";
                    return gift == node.BooleanValue;
                case RequirementNodeType.Title:
                    bool title = context.Identity != null && context.Identity.Titles.Any(value => value.titleDefinitionId == node.TargetId && value.active);
                    reason = title ? string.Empty : $"Title '{node.TargetId}' is missing.";
                    return title == node.BooleanValue;
                case RequirementNodeType.InventoryItem:
                    int quantity = CountInventory(context.Inventory, node.TargetId);
                    return Compare(quantity, node.IntegerValue, node.Comparison, out reason);
                case RequirementNodeType.EquippedItem:
                    bool equipped = IsEquipped(context.Equipment, node.TargetId);
                    reason = equipped ? string.Empty : $"Equipped item '{node.TargetId}' is missing.";
                    return equipped == node.BooleanValue;
                case RequirementNodeType.Ability:
                    bool ability = context.OwnedAbilityIds.Contains(node.TargetId);
                    reason = ability ? string.Empty : $"Ability/action '{node.TargetId}' is missing.";
                    return ability == node.BooleanValue;
                case RequirementNodeType.ConditionPresent:
                    bool present = HasStatus(context.Statuses, node.TargetId);
                    reason = present ? string.Empty : $"Condition/status '{node.TargetId}' is missing.";
                    return present == node.BooleanValue;
                case RequirementNodeType.ConditionAbsent:
                    bool absent = !HasStatus(context.Statuses, node.TargetId);
                    reason = absent ? string.Empty : $"Condition/status '{node.TargetId}' is present.";
                    return absent == node.BooleanValue;
                case RequirementNodeType.Currency:
                    long balance = context.CurrencyBalanceProvider == null ? 0L : context.CurrencyBalanceProvider(node.TargetId);
                    return Compare(balance, node.IntegerValue, node.Comparison, out reason);
                case RequirementNodeType.CapabilityBoolean:
                    CapabilitySnapshot boolCapability = context.Capabilities == null ? null : context.Capabilities.Evaluate(node.TargetId);
                    bool boolValue = boolCapability != null && !boolCapability.Blocked && boolCapability.BooleanValue;
                    reason = boolValue ? string.Empty : $"Capability '{node.TargetId}' is false or blocked.";
                    return boolValue == node.BooleanValue;
                case RequirementNodeType.CapabilityNumeric:
                    CapabilitySnapshot numericCapability = context.Capabilities == null ? null : context.Capabilities.Evaluate(node.TargetId);
                    return Compare(numericCapability == null || numericCapability.Blocked ? 0f : numericCapability.NumericValue, node.NumericValue, node.Comparison, out reason);
                case RequirementNodeType.Species:
                    bool species = context.Body != null && string.Equals(context.Body.SpeciesDefinitionId, node.TargetId, StringComparison.Ordinal);
                    reason = species ? string.Empty : $"Species '{node.TargetId}' is required.";
                    return species == node.BooleanValue;
                case RequirementNodeType.BiologicalClassification:
                    bool classification = context.Body?.BiologicalClassification != null && string.Equals(context.Body.BiologicalClassification.Id, node.TargetId, StringComparison.Ordinal);
                    reason = classification ? string.Empty : $"Biological classification '{node.TargetId}' is required.";
                    return classification == node.BooleanValue;
                case RequirementNodeType.BodyForm:
                    bool bodyForm = context.Body?.BodyForm != null && string.Equals(context.Body.BodyForm.Id, node.TargetId, StringComparison.Ordinal);
                    reason = bodyForm ? string.Empty : $"Body form '{node.TargetId}' is required.";
                    return bodyForm == node.BooleanValue;
                case RequirementNodeType.LifecycleState:
                    int actorLifecycleValue = context.Lifecycle == null ? -1 : (int)context.Lifecycle.State;
                    return Compare(actorLifecycleValue, node.IntegerValue, node.Comparison, out reason);
                case RequirementNodeType.BiologicalConditionPresent:
                    bool biologicalPresent = context.Body != null && context.Body.BiologicalConditions.CreateSnapshot().ActiveInstances.Any(value => string.Equals(value.ConditionDefinitionId, node.TargetId, StringComparison.Ordinal));
                    reason = biologicalPresent ? string.Empty : $"Biological condition '{node.TargetId}' is missing.";
                    return biologicalPresent == node.BooleanValue;
                case RequirementNodeType.BiologicalConditionAbsent:
                    bool biologicalAbsent = context.Body == null || context.Body.BiologicalConditions.CreateSnapshot().ActiveInstances.All(value => !string.Equals(value.ConditionDefinitionId, node.TargetId, StringComparison.Ordinal));
                    reason = biologicalAbsent ? string.Empty : $"Biological condition '{node.TargetId}' is present.";
                    return biologicalAbsent == node.BooleanValue;
                default:
                    reason = $"Unsupported requirement node type '{node.NodeType}'.";
                    return false;
            }
        }

        private static bool Compare(float actual, float expected, RequirementComparison comparison, out string reason)
        {
            bool passed = comparison switch
            {
                RequirementComparison.Equal => Math.Abs(actual - expected) <= EqualityTolerance,
                RequirementComparison.NotEqual => Math.Abs(actual - expected) > EqualityTolerance,
                RequirementComparison.GreaterThan => actual > expected,
                RequirementComparison.GreaterOrEqual => actual + EqualityTolerance >= expected,
                RequirementComparison.LessThan => actual < expected,
                RequirementComparison.LessOrEqual => actual <= expected + EqualityTolerance,
                _ => false
            };
            reason = passed ? string.Empty : $"Expected {actual:0.###} {comparison} {expected:0.###}.";
            return passed;
        }

        private static bool ContextMatches(RequirementNodeDefinition node, RequirementEvaluationContext context, RuntimeSocialStatusRecord status)
        {
            if (string.IsNullOrWhiteSpace(node.ContextKey))
            {
                return true;
            }

            return context.ContextIds != null
                && context.ContextIds.TryGetValue(node.ContextKey, out string expected)
                && string.Equals(status.contextTargetId ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal);
        }

        private static int CountInventory(PlayerInventory inventory, string itemId)
        {
            if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            int count = 0;
            foreach (InventorySlot slot in inventory.Slots)
            {
                if (slot != null && !slot.IsEmpty && slot.Item != null && string.Equals(slot.Item.Id, itemId, StringComparison.Ordinal))
                {
                    count += Math.Max(1, slot.Quantity);
                }
            }

            return count;
        }

        private static bool IsEquipped(PlayerEquipment equipment, string itemId)
        {
            return equipment != null && equipment.Slots.Any(slot => slot != null && !slot.IsEmpty && slot.Item != null && string.Equals(slot.Item.Id, itemId, StringComparison.Ordinal));
        }

        private static bool HasStatus(StatusEffectController statuses, string statusId)
        {
            return statuses != null && statuses.ActiveStatuses.Any(status => status.Definition != null && string.Equals(status.Definition.Id, statusId, StringComparison.Ordinal));
        }
    }
}
