using System;
using UnityEngine;

namespace UnityIsekaiGame.Stats
{
    [Serializable]
    public sealed class CalculatedStatModifierDefinition
    {
        [SerializeField] private CalculatedStatDefinition stat;
        [SerializeField] private StatModifierOperation operation;
        [SerializeField] private float value;
        [SerializeField] private bool scaleWithStacks = true;
        [SerializeField] private int priority;

        public CalculatedStatDefinition Stat => stat;
        public StatModifierOperation Operation => operation;
        public float Value => value;
        public bool ScaleWithStacks => scaleWithStacks;
        public int Priority => priority;
        public bool IsValid => stat != null && IsFinite(value);

        public RuntimeCalculatedStatContribution CreateRuntimeContribution(StatModifierSource source, int stackCount, string contributionId = "")
        {
            float effectiveValue = scaleWithStacks ? value * Mathf.Max(1, stackCount) : value;
            CalculatedStatContributionKind kind;
            CalculatedStatContributionDirection direction;
            float magnitude;
            switch (operation)
            {
                case StatModifierOperation.PercentAdd:
                    kind = CalculatedStatContributionKind.Percent;
                    direction = effectiveValue >= 0f ? CalculatedStatContributionDirection.Improve : CalculatedStatContributionDirection.Reduce;
                    magnitude = Mathf.Abs(effectiveValue);
                    break;
                case StatModifierOperation.Multiplicative:
                    kind = CalculatedStatContributionKind.Multiplier;
                    direction = effectiveValue >= 1f ? CalculatedStatContributionDirection.Improve : CalculatedStatContributionDirection.Reduce;
                    magnitude = effectiveValue >= 1f ? effectiveValue - 1f : 1f - Mathf.Max(0f, effectiveValue);
                    break;
                default:
                    kind = CalculatedStatContributionKind.Flat;
                    direction = effectiveValue >= 0f ? CalculatedStatContributionDirection.Improve : CalculatedStatContributionDirection.Reduce;
                    magnitude = Mathf.Abs(effectiveValue);
                    break;
            }

            return new RuntimeCalculatedStatContribution
            {
                contributionId = string.IsNullOrWhiteSpace(contributionId)
                    ? $"calculated-stat.{source.SourceType}.{source.SourceId}.{stat.Id}"
                    : contributionId,
                statId = stat.Id,
                sourceId = source.SourceId,
                sourceCategory = (int)CalculatedStatContributionSourceUtility.Map(source.SourceType),
                kind = (int)kind,
                direction = (int)direction,
                magnitude = magnitude,
                priority = priority
            };
        }

        private static bool IsFinite(float candidate) => !float.IsNaN(candidate) && !float.IsInfinity(candidate);
    }
}
