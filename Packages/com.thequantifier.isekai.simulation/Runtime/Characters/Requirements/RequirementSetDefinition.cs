using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;

namespace UnityIsekaiGame.Requirements
{
    [CreateAssetMenu(fileName = "RequirementSetDefinition", menuName = "Unity Isekai Game/Requirements/Requirement Set")]
    public sealed class RequirementSetDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string requirementSetId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private RequirementLogicalGroupDefinition rootGroup = new RequirementLogicalGroupDefinition();
        [SerializeField, Min(1)] private int maximumDepth = 6;
        [SerializeField] private bool alphaEnabled = true;

        public string Id => requirementSetId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Requirement;
        public RequirementLogicalGroupDefinition RootGroup => rootGroup;
        public int MaximumDepth => Math.Max(1, maximumDepth);
        public bool AlphaEnabled => alphaEnabled;

        private void OnValidate()
        {
            requirementSetId = requirementSetId?.Trim();
            maximumDepth = Math.Max(1, maximumDepth);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"RequirementSet '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("requirement.", StringComparison.Ordinal))
            {
                report.AddWarning($"RequirementSet '{Id}' should use the 'requirement.' namespace prefix.");
            }

            ValidateGroup(rootGroup, definitionsById, report, 0, new HashSet<string>(StringComparer.Ordinal));
        }

        private void ValidateGroup(RequirementLogicalGroupDefinition group, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, int depth, HashSet<string> seenGroups)
        {
            if (group == null)
            {
                report.AddError($"RequirementSet '{DisplayName}' has a missing logical group.");
                return;
            }

            if (depth > MaximumDepth)
            {
                report.AddError($"RequirementSet '{DisplayName}' exceeds maximum logical nesting depth {MaximumDepth}.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(group.GroupId) && !seenGroups.Add(group.GroupId))
            {
                report.AddError($"RequirementSet '{DisplayName}' has cyclic or duplicate group ID '{group.GroupId}'.");
            }

            if (group.Nodes.Count == 0 && group.Groups.Count == 0)
            {
                report.AddError($"RequirementSet '{DisplayName}' has an empty logical group.");
            }

            if (!Enum.IsDefined(typeof(RequirementLogicalOperator), group.LogicalOperator))
            {
                report.AddError($"RequirementSet '{DisplayName}' has an invalid logical operator.");
            }

            foreach (RequirementNodeDefinition node in group.Nodes)
            {
                ValidateNode(node, definitionsById, report);
            }

            foreach (RequirementLogicalGroupDefinition child in group.Groups)
            {
                ValidateGroup(child, definitionsById, report, depth + 1, seenGroups);
            }

            if (!string.IsNullOrWhiteSpace(group.GroupId))
            {
                seenGroups.Remove(group.GroupId);
            }
        }

        private void ValidateNode(RequirementNodeDefinition node, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (node == null)
            {
                report.AddError($"RequirementSet '{DisplayName}' has a missing requirement node.");
                return;
            }

            if (!Enum.IsDefined(typeof(RequirementNodeType), node.NodeType) || !Enum.IsDefined(typeof(RequirementComparison), node.Comparison) || !Enum.IsDefined(typeof(RequirementFailureVisibility), node.FailureVisibility))
            {
                report.AddError($"RequirementSet '{DisplayName}' node '{node.NodeId}' has an invalid enum value.");
            }

            if (node.RequiresContext && string.IsNullOrWhiteSpace(node.ContextKey))
            {
                report.AddError($"RequirementSet '{DisplayName}' node '{node.NodeId}' requires context but does not declare a context key.");
            }

            if (node.NodeType != RequirementNodeType.LifecycleState && node.TargetDefinition == null)
            {
                report.AddError($"RequirementSet '{DisplayName}' node '{node.NodeId}' is missing its typed target definition.");
                return;
            }

            if (definitionsById == null || string.IsNullOrWhiteSpace(node.TargetId))
            {
                return;
            }

            Type expected = ExpectedDefinitionType(node.NodeType);
            if (expected == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(node.TargetId, out IGameDefinition found) || !expected.IsInstanceOfType(found))
            {
                report.AddError($"RequirementSet '{DisplayName}' node '{node.NodeId}' references '{node.TargetId}', which is not a configured {expected.Name}.");
            }
        }

        private static Type ExpectedDefinitionType(RequirementNodeType nodeType)
        {
            return nodeType switch
            {
                RequirementNodeType.TraitLifecycle => typeof(TraitDefinition),
                RequirementNodeType.CapabilityBoolean or RequirementNodeType.CapabilityNumeric => typeof(CapabilityDefinition),
                RequirementNodeType.Species => typeof(SpeciesDefinition),
                RequirementNodeType.BiologicalClassification => typeof(BiologicalClassificationDefinition),
                RequirementNodeType.BodyForm => typeof(BodyFormDefinition),
                RequirementNodeType.BiologicalConditionPresent or RequirementNodeType.BiologicalConditionAbsent => typeof(BiologicalConditionDefinition),
                _ => null
            };
        }
    }
}
