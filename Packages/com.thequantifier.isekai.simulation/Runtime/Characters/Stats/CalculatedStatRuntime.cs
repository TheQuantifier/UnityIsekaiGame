using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Stats
{
    [Serializable]
    public sealed class RuntimeCalculatedStatContribution
    {
        public string contributionId;
        public string statId;
        public string sourceId;
        public int sourceCategory;
        public int kind;
        public int direction;
        public float magnitude;
        public int priority;
    }

    public sealed class CalculatedStatEvaluationBreakdown
    {
        public string StatId { get; set; }
        public float BaseValue { get; set; }
        public float AttributeWeightedTotal { get; set; }
        public float PositiveFlatTotal { get; set; }
        public float NegativeFlatTotal { get; set; }
        public float PositivePercentTotal { get; set; }
        public float NegativePercentTotal { get; set; }
        public float PositiveMultiplier { get; set; } = 1f;
        public float ReducingMultiplier { get; set; } = 1f;
        public float RawValueBeforeClamp { get; set; }
        public float ClampedValue { get; set; }
        public float FinalValue { get; set; }
        public IReadOnlyList<RuntimeCalculatedStatContribution> Contributions { get; set; } = Array.Empty<RuntimeCalculatedStatContribution>();
    }
}
