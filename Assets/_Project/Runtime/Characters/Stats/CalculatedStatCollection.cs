using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Stats
{
    public sealed class CalculatedStatCollection : MonoBehaviour
    {
        [SerializeField] private CharacterAttributes attributes;

        private readonly Dictionary<string, CalculatedStatDefinition> definitionsById = new Dictionary<string, CalculatedStatDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> cachedValues = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, CalculatedStatEvaluationBreakdown> cachedBreakdowns = new Dictionary<string, CalculatedStatEvaluationBreakdown>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<RuntimeCalculatedStatContribution>> contributionsBySourceKey = new Dictionary<string, List<RuntimeCalculatedStatContribution>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> dependencyStatIdsByAttributeId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private bool recalculating;

        public event Action<CalculatedStatCollection, IReadOnlyList<string>, bool> CalculatedStatsChanged;

        public CharacterAttributes Attributes => attributes;
        public bool IsConfigured { get; private set; }
        public IReadOnlyCollection<CalculatedStatDefinition> Definitions => definitionsById.Values;

        private void Awake()
        {
            if (attributes == null)
            {
                attributes = GetComponent<CharacterAttributes>();
            }

        }

        private void OnEnable()
        {
            if (attributes != null)
            {
                attributes.AttributesChanged += OnAttributesChanged;
            }
        }

        private void OnDisable()
        {
            if (attributes != null)
            {
                attributes.AttributesChanged -= OnAttributesChanged;
            }
        }

        public void Configure(DefinitionRegistry registry, CharacterAttributes attributeSource = null)
        {
            Configure(registry == null
                ? Enumerable.Empty<CalculatedStatDefinition>()
                : registry.DefinitionsById.Values.OfType<CalculatedStatDefinition>(),
                attributeSource);
        }

        public void Configure(IEnumerable<CalculatedStatDefinition> definitions, CharacterAttributes attributeSource = null)
        {
            if (attributes != null)
            {
                attributes.AttributesChanged -= OnAttributesChanged;
            }

            attributes = attributeSource == null ? attributes == null ? GetComponent<CharacterAttributes>() : attributes : attributeSource;
            if (attributes != null)
            {
                attributes.AttributesChanged += OnAttributesChanged;
            }

            definitionsById.Clear();
            dependencyStatIdsByAttributeId.Clear();
            foreach (CalculatedStatDefinition definition in definitions ?? Enumerable.Empty<CalculatedStatDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || definitionsById.ContainsKey(definition.Id))
                {
                    continue;
                }

                definitionsById.Add(definition.Id, definition);
                foreach (AttributeFormulaTerm term in definition.Formula == null ? Array.Empty<AttributeFormulaTerm>() : definition.Formula.AttributeTerms)
                {
                    if (term?.Attribute == null)
                    {
                        continue;
                    }

                    if (!dependencyStatIdsByAttributeId.TryGetValue(term.Attribute.Id, out HashSet<string> dependents))
                    {
                        dependents = new HashSet<string>(StringComparer.Ordinal);
                        dependencyStatIdsByAttributeId.Add(term.Attribute.Id, dependents);
                    }

                    dependents.Add(definition.Id);
                }
            }

            IsConfigured = definitionsById.Count > 0;
            ForceRecalculateAll(false);
        }

        public bool HasStat(string statId)
        {
            return definitionsById.ContainsKey(statId);
        }

        public float GetValue(string statId)
        {
            return cachedValues.TryGetValue(statId, out float value) ? value : 0f;
        }

        public CalculatedStatEvaluationBreakdown GetBreakdown(string statId)
        {
            return cachedBreakdowns.TryGetValue(statId, out CalculatedStatEvaluationBreakdown breakdown) ? breakdown : null;
        }

        public IReadOnlyList<CalculatedStatDefinition> GetOrderedDefinitions(bool characterMenuOnly)
        {
            return definitionsById.Values
                .Where(definition => !characterMenuOnly || definition.ExposedOnCharacterMenu)
                .OrderBy(definition => definition.SortOrder)
                .ThenBy(definition => definition.DisplayName)
                .ToList();
        }

        public bool AddContribution(RuntimeCalculatedStatContribution contribution, out string failureReason, bool restoring = false)
        {
            failureReason = string.Empty;
            if (!ValidateContribution(contribution, out failureReason))
            {
                return false;
            }

            string key = SourceKey(contribution.sourceCategory, contribution.sourceId);
            if (!contributionsBySourceKey.TryGetValue(key, out List<RuntimeCalculatedStatContribution> list))
            {
                list = new List<RuntimeCalculatedStatContribution>();
                contributionsBySourceKey.Add(key, list);
            }

            if (list.Any(existing => string.Equals(existing.statId, contribution.statId, StringComparison.Ordinal)
                && existing.kind == contribution.kind
                && existing.direction == contribution.direction
                && existing.priority == contribution.priority))
            {
                failureReason = $"Calculated stat contribution from '{contribution.sourceId}' to '{contribution.statId}' is already registered.";
                return false;
            }

            list.Add(CloneContribution(contribution));
            RecalculateChanged(new[] { contribution.statId }, restoring);
            return true;
        }

        public bool RemoveContributionsFromSource(CalculatedStatContributionSourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            string key = SourceKey((int)sourceCategory, sourceId);
            if (!contributionsBySourceKey.TryGetValue(key, out List<RuntimeCalculatedStatContribution> list))
            {
                return false;
            }

            List<string> changed = list.Select(contribution => contribution.statId).Distinct(StringComparer.Ordinal).ToList();
            contributionsBySourceKey.Remove(key);
            RecalculateChanged(changed, restoring);
            return true;
        }

        public void ClearContributions(bool restoring = false)
        {
            List<string> changed = contributionsBySourceKey.Values
                .SelectMany(list => list)
                .Select(contribution => contribution.statId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            contributionsBySourceKey.Clear();
            RecalculateChanged(changed, restoring);
        }

        public void ForceRecalculateAll(bool restoring = false)
        {
            if (recalculating)
            {
                return;
            }

            recalculating = true;
            try
            {
                List<string> changed = new List<string>();
                foreach (string statId in definitionsById.Keys.ToList())
                {
                    float previous = GetValue(statId);
                    CalculatedStatEvaluationBreakdown breakdown = Evaluate(statId);
                    cachedValues[statId] = breakdown.FinalValue;
                    cachedBreakdowns[statId] = breakdown;
                    if (!Mathf.Approximately(previous, breakdown.FinalValue))
                    {
                        changed.Add(statId);
                    }
                }

                if (changed.Count > 0)
                {
                    CalculatedStatsChanged?.Invoke(this, changed, restoring);
                }
            }
            finally
            {
                recalculating = false;
            }
        }

        public string BuildDiagnosticSummary()
        {
            List<string> lines = new List<string> { "Feature 5.4a Calculated Stats" };
            foreach (CalculatedStatDefinition definition in GetOrderedDefinitions(characterMenuOnly: false))
            {
                CalculatedStatEvaluationBreakdown breakdown = GetBreakdown(definition.Id);
                string resource = definition.IsResourceMaximum ? $" Resource={definition.LinkedResourceId}" : string.Empty;
                lines.Add($"{definition.DisplayName}: {GetValue(definition.Id):0.###} ({definition.Id}) Purpose={definition.Purpose}{resource} Base={breakdown?.BaseValue ?? 0f:0.###} Attr={breakdown?.AttributeWeightedTotal ?? 0f:0.###} +Flat={breakdown?.PositiveFlatTotal ?? 0f:0.###} -Flat={breakdown?.NegativeFlatTotal ?? 0f:0.###}");
            }

            lines.Add($"Contribution Sources: {contributionsBySourceKey.Count}");
            return string.Join(Environment.NewLine, lines);
        }

        private void RecalculateChanged(IReadOnlyList<string> statIds, bool restoring)
        {
            if (recalculating)
            {
                return;
            }

            recalculating = true;
            try
            {
                List<string> changed = new List<string>();
                foreach (string statId in statIds ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(statId) || !definitionsById.ContainsKey(statId))
                    {
                        continue;
                    }

                    float previous = GetValue(statId);
                    CalculatedStatEvaluationBreakdown breakdown = Evaluate(statId);
                    cachedValues[statId] = breakdown.FinalValue;
                    cachedBreakdowns[statId] = breakdown;
                    if (!Mathf.Approximately(previous, breakdown.FinalValue))
                    {
                        changed.Add(statId);
                    }
                }

                if (changed.Count > 0)
                {
                    CalculatedStatsChanged?.Invoke(this, changed, restoring);
                }
            }
            finally
            {
                recalculating = false;
            }
        }

        private CalculatedStatEvaluationBreakdown Evaluate(string statId)
        {
            CalculatedStatDefinition definition = definitionsById[statId];
            CalculatedStatFormulaDefinition formula = definition.Formula;
            float attributeTotal = 0f;
            if (formula != null)
            {
                foreach (AttributeFormulaTerm term in formula.AttributeTerms)
                {
                    if (term?.Attribute == null)
                    {
                        continue;
                    }

                    attributeTotal += (attributes == null ? 0f : attributes.GetValue(term.Attribute.Id)) * term.Weight;
                }
            }

            List<RuntimeCalculatedStatContribution> contributions = GetContributions(statId);
            float positiveFlat = 0f;
            float negativeFlat = 0f;
            float positivePercent = 0f;
            float negativePercent = 0f;
            float positiveMultiplier = 1f;
            float reducingMultiplier = 1f;

            foreach (RuntimeCalculatedStatContribution contribution in contributions)
            {
                float magnitude = Mathf.Abs(contribution.magnitude);
                CalculatedStatContributionKind kind = (CalculatedStatContributionKind)contribution.kind;
                CalculatedStatContributionDirection direction = (CalculatedStatContributionDirection)contribution.direction;
                if (kind == CalculatedStatContributionKind.Flat)
                {
                    if (direction == CalculatedStatContributionDirection.Improve)
                    {
                        positiveFlat += magnitude;
                    }
                    else
                    {
                        negativeFlat += magnitude;
                    }
                }
                else if (kind == CalculatedStatContributionKind.Percent)
                {
                    if (direction == CalculatedStatContributionDirection.Improve)
                    {
                        positivePercent += magnitude;
                    }
                    else
                    {
                        negativePercent += magnitude;
                    }
                }
                else if (kind == CalculatedStatContributionKind.Multiplier)
                {
                    if (direction == CalculatedStatContributionDirection.Improve)
                    {
                        positiveMultiplier *= 1f + magnitude;
                    }
                    else
                    {
                        reducingMultiplier *= Mathf.Max(0f, 1f - magnitude);
                    }
                }
            }

            float formulaBase = formula == null ? 0f : formula.BaseValue;
            float baseAfterFlats = formulaBase + attributeTotal + positiveFlat - negativeFlat;
            float percentFactor = Mathf.Max(0f, 1f + positivePercent - negativePercent);
            float raw = baseAfterFlats * percentFactor * positiveMultiplier * reducingMultiplier;
            float clamped = formula == null || formula.ClampMinimumToZero ? Mathf.Max(0f, raw) : raw;
            float final = formula == null
                ? clamped
                : formula.RoundingPolicy switch
                {
                    CalculatedStatRoundingPolicy.None => clamped,
                    CalculatedStatRoundingPolicy.Floor => Mathf.Floor(clamped),
                    CalculatedStatRoundingPolicy.Ceiling => Mathf.Ceil(clamped),
                    _ => Mathf.Round(clamped)
                };

            return new CalculatedStatEvaluationBreakdown
            {
                StatId = statId,
                BaseValue = formulaBase,
                AttributeWeightedTotal = attributeTotal,
                PositiveFlatTotal = positiveFlat,
                NegativeFlatTotal = negativeFlat,
                PositivePercentTotal = positivePercent,
                NegativePercentTotal = negativePercent,
                PositiveMultiplier = positiveMultiplier,
                ReducingMultiplier = reducingMultiplier,
                RawValueBeforeClamp = raw,
                ClampedValue = clamped,
                FinalValue = final,
                Contributions = contributions
            };
        }

        private List<RuntimeCalculatedStatContribution> GetContributions(string statId)
        {
            return contributionsBySourceKey.Values
                .SelectMany(list => list)
                .Where(contribution => string.Equals(contribution.statId, statId, StringComparison.Ordinal))
                .OrderBy(contribution => contribution.priority)
                .Select(CloneContribution)
                .ToList();
        }

        private bool ValidateContribution(RuntimeCalculatedStatContribution contribution, out string failureReason)
        {
            failureReason = string.Empty;
            if (contribution == null)
            {
                failureReason = "Calculated stat contribution is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contribution.statId) || !definitionsById.ContainsKey(contribution.statId))
            {
                failureReason = $"Calculated stat '{contribution.statId}' is not configured.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contribution.sourceId))
            {
                failureReason = "Calculated stat contribution source ID is missing.";
                return false;
            }

            if (!IsFinite(contribution.magnitude))
            {
                failureReason = "Calculated stat contribution magnitude must be finite.";
                return false;
            }

            if (!Enum.IsDefined(typeof(CalculatedStatContributionKind), contribution.kind)
                || !Enum.IsDefined(typeof(CalculatedStatContributionDirection), contribution.direction)
                || !Enum.IsDefined(typeof(CalculatedStatContributionSourceCategory), contribution.sourceCategory))
            {
                failureReason = "Calculated stat contribution has an invalid enum value.";
                return false;
            }

            return true;
        }

        private void OnAttributesChanged(CharacterAttributes source, IReadOnlyList<string> attributeIds, bool restoring)
        {
            HashSet<string> statIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string attributeId in attributeIds ?? Array.Empty<string>())
            {
                if (dependencyStatIdsByAttributeId.TryGetValue(attributeId, out HashSet<string> dependents))
                {
                    statIds.UnionWith(dependents);
                }
            }

            RecalculateChanged(statIds.ToList(), restoring);
        }

        private static string SourceKey(int sourceCategory, string sourceId)
        {
            return $"{sourceCategory}:{sourceId ?? string.Empty}";
        }

        public static RuntimeCalculatedStatContribution CloneContribution(RuntimeCalculatedStatContribution contribution)
        {
            return contribution == null
                ? null
                : new RuntimeCalculatedStatContribution
                {
                    contributionId = contribution.contributionId,
                    statId = contribution.statId,
                    sourceId = contribution.sourceId,
                    sourceCategory = contribution.sourceCategory,
                    kind = contribution.kind,
                    direction = contribution.direction,
                    magnitude = contribution.magnitude,
                    priority = contribution.priority
                };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
