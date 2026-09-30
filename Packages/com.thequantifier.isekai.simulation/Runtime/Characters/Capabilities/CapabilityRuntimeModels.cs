using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Capabilities
{
    [Serializable]
    public sealed class RuntimeCapabilityContribution
    {
        public string capabilityId;
        public int valueType;
        public bool boolValue;
        public float numericValue;
        public int aggregationPolicy;
        public int sourceCategory;
        public string sourceId;
        public string entryId;
        public int priority;
        public bool blocker;
    }

    public sealed class CapabilitySnapshot
    {
        public string CapabilityId { get; set; }
        public CapabilityValueType ValueType { get; set; }
        public bool BooleanValue { get; set; }
        public float NumericValue { get; set; }
        public bool Blocked { get; set; }
        public IReadOnlyList<RuntimeCapabilityContribution> Sources { get; set; } = Array.Empty<RuntimeCapabilityContribution>();
    }

    public sealed class RuntimeCapabilitySet
    {
        private readonly Dictionary<string, CapabilityDefinition> definitionsById = new Dictionary<string, CapabilityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeCapabilityContribution> contributionsByKey = new Dictionary<string, RuntimeCapabilityContribution>(StringComparer.Ordinal);

        public IReadOnlyCollection<RuntimeCapabilityContribution> Contributions => contributionsByKey.Values.Select(Clone).ToList();

        public void Configure(IEnumerable<CapabilityDefinition> definitions)
        {
            definitionsById.Clear();
            foreach (CapabilityDefinition definition in definitions ?? Array.Empty<CapabilityDefinition>())
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id) && !definitionsById.ContainsKey(definition.Id))
                {
                    definitionsById.Add(definition.Id, definition);
                }
            }
        }

        public void ClearSource(CapabilitySourceCategory sourceCategory, string sourceId)
        {
            string prefix = $"{(int)sourceCategory}:{sourceId ?? string.Empty}:";
            foreach (string key in contributionsByKey.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                contributionsByKey.Remove(key);
            }
        }

        public bool Add(RuntimeCapabilityContribution contribution)
        {
            if (contribution == null || string.IsNullOrWhiteSpace(contribution.capabilityId) || string.IsNullOrWhiteSpace(contribution.sourceId))
            {
                return false;
            }

            contributionsByKey[Key(contribution)] = Clone(contribution);
            return true;
        }

        public CapabilitySnapshot Evaluate(string capabilityId)
        {
            if (string.IsNullOrWhiteSpace(capabilityId))
            {
                return new CapabilitySnapshot();
            }

            definitionsById.TryGetValue(capabilityId, out CapabilityDefinition definition);
            CapabilityValueType valueType = definition == null ? CapabilityValueType.Boolean : definition.ValueType;
            CapabilityAggregationPolicy policy = definition == null ? CapabilityAggregationPolicy.BooleanAny : definition.AggregationPolicy;
            List<RuntimeCapabilityContribution> sources = contributionsByKey.Values
                .Where(value => string.Equals(value.capabilityId, capabilityId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList();

            bool blocked = sources.Any(source => source.blocker || (CapabilityAggregationPolicy)source.aggregationPolicy == CapabilityAggregationPolicy.Blocker);
            bool boolValue = definition != null && definition.DefaultBooleanValue;
            float numericValue = definition == null ? 0f : definition.DefaultNumericValue;

            if (!blocked && valueType == CapabilityValueType.Boolean)
            {
                boolValue = boolValue || sources.Any(source => source.boolValue);
            }
            else if (!blocked)
            {
                switch (policy)
                {
                    case CapabilityAggregationPolicy.Highest:
                        numericValue = sources.Count == 0 ? numericValue : Math.Max(numericValue, sources.Max(source => source.numericValue));
                        break;
                    case CapabilityAggregationPolicy.PriorityOverride:
                        RuntimeCapabilityContribution winner = sources
                            .OrderByDescending(source => source.priority)
                            .ThenBy(source => source.entryId, StringComparer.Ordinal)
                            .FirstOrDefault();
                        numericValue = winner == null ? numericValue : winner.numericValue;
                        break;
                    case CapabilityAggregationPolicy.Sum:
                    default:
                        numericValue += sources.Sum(source => source.numericValue);
                        break;
                }

                if (definition != null)
                {
                    numericValue = Math.Max(definition.MinimumValue, Math.Min(definition.MaximumValue, numericValue));
                }
            }

            return new CapabilitySnapshot
            {
                CapabilityId = capabilityId,
                ValueType = valueType,
                BooleanValue = !blocked && boolValue,
                NumericValue = blocked ? 0f : numericValue,
                Blocked = blocked,
                Sources = sources
            };
        }

        public IReadOnlyList<CapabilitySnapshot> GetSnapshots()
        {
            HashSet<string> ids = new HashSet<string>(definitionsById.Keys, StringComparer.Ordinal);
            foreach (RuntimeCapabilityContribution contribution in contributionsByKey.Values)
            {
                ids.Add(contribution.capabilityId);
            }

            return ids.OrderBy(id => id, StringComparer.Ordinal).Select(Evaluate).ToList();
        }

        private static string Key(RuntimeCapabilityContribution contribution)
        {
            return $"{contribution.sourceCategory}:{contribution.sourceId ?? string.Empty}:{contribution.capabilityId}:{contribution.entryId ?? string.Empty}";
        }

        public static RuntimeCapabilityContribution Clone(RuntimeCapabilityContribution contribution)
        {
            return contribution == null
                ? null
                : new RuntimeCapabilityContribution
                {
                    capabilityId = contribution.capabilityId,
                    valueType = contribution.valueType,
                    boolValue = contribution.boolValue,
                    numericValue = contribution.numericValue,
                    aggregationPolicy = contribution.aggregationPolicy,
                    sourceCategory = contribution.sourceCategory,
                    sourceId = contribution.sourceId,
                    entryId = contribution.entryId,
                    priority = contribution.priority,
                    blocker = contribution.blocker
                };
        }
    }
}
