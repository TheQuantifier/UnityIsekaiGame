using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Capabilities
{
    /// <summary>
    /// Canonical, source-aware capability state for a character. Traits, species,
    /// skills, roles, equipment, and statuses contribute here instead of owning
    /// parallel capability stores.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCapabilityCollection : MonoBehaviour
    {
        private readonly RuntimeCapabilitySet runtime = new RuntimeCapabilitySet();
        private readonly Dictionary<string, CapabilityDefinition> definitionsById = new Dictionary<string, CapabilityDefinition>(StringComparer.Ordinal);

        public event Action<CharacterCapabilityCollection, string, bool> CapabilitiesChanged;
        public bool IsConfigured { get; private set; }
        public IReadOnlyCollection<RuntimeCapabilityContribution> Contributions => runtime.Contributions;

        public void Configure(DefinitionRegistry registry)
        {
            definitionsById.Clear();
            ConfigureDefinitions(registry == null ? Array.Empty<CapabilityDefinition>() : registry.DefinitionsById.Values.OfType<CapabilityDefinition>());
        }

        public void Configure(IEnumerable<CapabilityDefinition> definitions)
        {
            definitionsById.Clear();
            ConfigureDefinitions(definitions);
        }

        private void ConfigureDefinitions(IEnumerable<CapabilityDefinition> definitions)
        {
            foreach (CapabilityDefinition definition in definitions ?? Array.Empty<CapabilityDefinition>())
            {
                if (definition != null && definition.AlphaEnabled && !string.IsNullOrWhiteSpace(definition.Id) && !definitionsById.ContainsKey(definition.Id))
                {
                    definitionsById.Add(definition.Id, definition);
                }
            }

            runtime.Configure(definitionsById.Values);
            IsConfigured = definitionsById.Count > 0;
        }

        public bool Add(RuntimeCapabilityContribution contribution, bool restoring = false)
        {
            if (contribution == null || !definitionsById.ContainsKey(contribution.capabilityId))
            {
                return false;
            }

            bool changed = runtime.Add(contribution);
            if (changed)
            {
                CapabilitiesChanged?.Invoke(this, contribution.capabilityId, restoring);
            }
            return changed;
        }

        public bool Add(
            CapabilityDefinition capability,
            CapabilitySourceCategory sourceCategory,
            string sourceId,
            string entryId,
            bool booleanValue = true,
            float numericValue = 0f,
            int priority = 0,
            bool blocker = false,
            bool restoring = false)
        {
            if (capability == null)
            {
                return false;
            }

            return Add(new RuntimeCapabilityContribution
            {
                capabilityId = capability.Id,
                valueType = (int)capability.ValueType,
                boolValue = booleanValue,
                numericValue = numericValue,
                aggregationPolicy = (int)capability.AggregationPolicy,
                sourceCategory = (int)sourceCategory,
                sourceId = sourceId,
                entryId = entryId,
                priority = priority,
                blocker = blocker
            }, restoring);
        }

        public bool RemoveSource(CapabilitySourceCategory sourceCategory, string sourceId, bool restoring = false)
        {
            string[] affected = runtime.Contributions
                .Where(value => value.sourceCategory == (int)sourceCategory && string.Equals(value.sourceId, sourceId, StringComparison.Ordinal))
                .Select(value => value.capabilityId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (affected.Length == 0)
            {
                return false;
            }

            runtime.ClearSource(sourceCategory, sourceId);
            foreach (string capabilityId in affected)
            {
                CapabilitiesChanged?.Invoke(this, capabilityId, restoring);
            }
            return true;
        }

        public CapabilitySnapshot Evaluate(string capabilityId) => runtime.Evaluate(capabilityId);
        public bool Has(string capabilityId) => Evaluate(capabilityId).BooleanValue;
        public IReadOnlyList<CapabilitySnapshot> GetSnapshots() => runtime.GetSnapshots();
    }
}
