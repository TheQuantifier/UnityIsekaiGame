using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Requirements
{
    [Serializable]
    public sealed class RequirementNodeDefinition
    {
        [SerializeField] private string nodeId;
        [SerializeField] private RequirementNodeType nodeType;
        [SerializeField] private RequirementComparison comparison = RequirementComparison.GreaterOrEqual;
        [SerializeField] private ScriptableObject targetDefinition;
        [SerializeField] private float numericValue;
        [SerializeField] private int integerValue;
        [SerializeField] private bool booleanValue = true;
        [SerializeField] private string contextKey;
        [SerializeField] private RequirementFailureVisibility failureVisibility = RequirementFailureVisibility.Visible;
        [SerializeField] private string visibleFailureReason;
        [SerializeField] private string obscuredFailureReason = "Requirement not met.";
        [SerializeField] private bool requiresContext;

        public string NodeId => nodeId ?? string.Empty;
        public RequirementNodeType NodeType => nodeType;
        public RequirementComparison Comparison => comparison;
        public ScriptableObject TargetDefinition => targetDefinition;
        public string TargetId => targetDefinition is IGameDefinition definition ? definition.Id : string.Empty;
        public float NumericValue => numericValue;
        public int IntegerValue => integerValue;
        public bool BooleanValue => booleanValue;
        public string ContextKey => contextKey ?? string.Empty;
        public RequirementFailureVisibility FailureVisibility => failureVisibility;
        public string VisibleFailureReason => string.IsNullOrWhiteSpace(visibleFailureReason) ? $"Requirement '{NodeId}' failed." : visibleFailureReason;
        public string ObscuredFailureReason => string.IsNullOrWhiteSpace(obscuredFailureReason) ? "Requirement not met." : obscuredFailureReason;
        public bool RequiresContext => requiresContext;
    }

    [Serializable]
    public sealed class RequirementLogicalGroupDefinition
    {
        [SerializeField] private string groupId;
        [SerializeField] private RequirementLogicalOperator logicalOperator = RequirementLogicalOperator.All;
        [SerializeField] private RequirementNodeDefinition[] nodes;
        [SerializeReference] private RequirementLogicalGroupDefinition[] groups;

        public string GroupId => groupId ?? string.Empty;
        public RequirementLogicalOperator LogicalOperator => logicalOperator;
        public IReadOnlyList<RequirementNodeDefinition> Nodes => nodes ?? Array.Empty<RequirementNodeDefinition>();
        public IReadOnlyList<RequirementLogicalGroupDefinition> Groups => groups ?? Array.Empty<RequirementLogicalGroupDefinition>();
    }

    public sealed class RequirementNodeResult
    {
        public string NodeId { get; set; }
        public RequirementNodeType NodeType { get; set; }
        public bool Passed { get; set; }
        public RequirementFailureVisibility FailureVisibility { get; set; }
        public string InternalReason { get; set; }
        public string PlayerFacingReason { get; set; }
    }

    public sealed class RequirementEvaluationResult
    {
        public bool Passed { get; set; }
        public string RequirementSetId { get; set; }
        public List<RequirementNodeResult> NodeResults { get; } = new List<RequirementNodeResult>();
        public IReadOnlyList<string> VisibleFailureReasons => BuildReasons(RequirementFailureVisibility.Visible);
        public IReadOnlyList<string> TestLabFailureReasons => BuildReasons(null);

        private IReadOnlyList<string> BuildReasons(RequirementFailureVisibility? visibility)
        {
            List<string> reasons = new List<string>();
            foreach (RequirementNodeResult node in NodeResults)
            {
                if (node.Passed)
                {
                    continue;
                }

                if (visibility.HasValue && node.FailureVisibility != visibility.Value)
                {
                    continue;
                }

                string reason = visibility.HasValue ? node.PlayerFacingReason : node.InternalReason;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    reasons.Add(reason);
                }
            }

            return reasons;
        }
    }
}
