using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    [CreateAssetMenu(fileName = "BiologicalCompatibilityProfile", menuName = "Unity Isekai Game/Beings/Biology/Biological Compatibility Profile")]
    public sealed class BiologicalCompatibilityProfileDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private SpeciesDefinition speciesDefinition;
        [SerializeField] private BodyFormDefinition bodyFormDefinition;
        [SerializeField] private BiologicalClassificationDefinition biologicalClassification;
        [SerializeField] private BiologicalInteractionRuleDefinition[] rules;
        [SerializeField] private bool alphaEnabled = true;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SpeciesDefinition SpeciesDefinition => speciesDefinition;
        public BodyFormDefinition BodyFormDefinition => bodyFormDefinition;
        public BiologicalClassificationDefinition BiologicalClassification => biologicalClassification;
        public string SpeciesDefinitionId => speciesDefinition == null ? string.Empty : speciesDefinition.Id;
        public string BodyFormDefinitionId => bodyFormDefinition == null ? string.Empty : bodyFormDefinition.Id;
        public string BiologicalClassificationId => biologicalClassification == null ? string.Empty : biologicalClassification.Id;
        public IReadOnlyList<BiologicalInteractionRuleDefinition> Rules => rules ?? Array.Empty<BiologicalInteractionRuleDefinition>();
        public bool AlphaEnabled => alphaEnabled;

        private void OnValidate()
        {
            profileId = profileId?.Trim();
        }

        public bool AppliesTo(BodySnapshot body)
        {
            if (body == null || !AlphaEnabled)
            {
                return false;
            }

            bool anySelector = false;
            if (!string.IsNullOrWhiteSpace(SpeciesDefinitionId))
            {
                anySelector = true;
                if (!string.Equals(SpeciesDefinitionId, body.SpeciesId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(BodyFormDefinitionId))
            {
                anySelector = true;
                if (!string.Equals(BodyFormDefinitionId, body.BodyFormId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(BiologicalClassificationId))
            {
                anySelector = true;
                if (!string.Equals(BiologicalClassificationId, body.BiologicalClassificationId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return anySelector;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"BiologicalCompatibilityProfileDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("compatibility-profile.", StringComparison.Ordinal))
            {
                report.AddWarning($"BiologicalCompatibilityProfileDefinition '{Id}' should use the 'compatibility-profile.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(SpeciesDefinitionId) && string.IsNullOrWhiteSpace(BodyFormDefinitionId) && string.IsNullOrWhiteSpace(BiologicalClassificationId))
            {
                report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' must target a Species, body form, or biological classification.");
            }

            ValidateReference(definitionsById, speciesDefinition, "Species", report);
            ValidateReference(definitionsById, bodyFormDefinition, "Body form", report);
            ValidateReference(definitionsById, biologicalClassification, "Biological classification", report);
            ValidateRules(definitionsById, report);
            ValidateCanonicalProfiles(definitionsById, report);
        }

        private void ValidateRules(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> conversionsByEntry = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BiologicalInteractionRuleDefinition rule in Rules.Where(rule => rule != null && rule.AlphaEnabled))
            {
                if (string.IsNullOrWhiteSpace(rule.EntryId))
                {
                    report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' has a rule with a missing entryId.");
                }
                else if (!ids.Add(rule.EntryId))
                {
                    report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' has duplicate rule entryId '{rule.EntryId}'.");
                }

                if (string.IsNullOrWhiteSpace(rule.InteractionDefinitionId) && rule.Category == BiologicalInteractionCategory.Unknown)
                {
                    report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' rule '{rule.EntryId}' must target an interaction ID or category.");
                }

                if (!string.IsNullOrWhiteSpace(rule.InteractionDefinitionId)
                    && definitionsById != null
                    && (!definitionsById.TryGetValue(rule.InteractionDefinitionId, out IGameDefinition definition) || definition is not BiologicalInteractionDefinition))
                {
                    report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' rule '{rule.EntryId}' references unknown interaction '{rule.InteractionDefinitionId}'.");
                }

                ValidateRuleOutcome(definitionsById, rule, conversionsByEntry, report);
                ValidateCapabilities(definitionsById, rule.RequiredCapabilities, rule.EntryId, "required", report);
                ValidateCapabilities(definitionsById, rule.BlockingCapabilities, rule.EntryId, "blocking", report);
            }

            ValidateConversionGraph(conversionsByEntry, report);
        }

        private void ValidateRuleOutcome(IReadOnlyDictionary<string, IGameDefinition> definitionsById, BiologicalInteractionRuleDefinition rule, Dictionary<string, string> conversionsByEntry, DefinitionValidationReport report)
        {
            if (rule.RuleKind == BiologicalInteractionRuleKind.Conversion)
            {
                if (string.IsNullOrWhiteSpace(rule.ConvertedInteractionDefinitionId))
                {
                    report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' conversion rule '{rule.EntryId}' is missing a converted interaction ID.");
                }
                else
                {
                    if (string.Equals(rule.InteractionDefinitionId, rule.ConvertedInteractionDefinitionId, StringComparison.Ordinal))
                    {
                        report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' conversion rule '{rule.EntryId}' converts an interaction to itself.");
                    }

                    if (definitionsById != null
                        && (!definitionsById.TryGetValue(rule.ConvertedInteractionDefinitionId, out IGameDefinition convertedDefinition) || convertedDefinition is not BiologicalInteractionDefinition))
                    {
                        report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' conversion rule '{rule.EntryId}' references unknown converted interaction '{rule.ConvertedInteractionDefinitionId}'.");
                    }

                    if (!string.IsNullOrWhiteSpace(rule.InteractionDefinitionId))
                    {
                        conversionsByEntry[rule.InteractionDefinitionId] = rule.ConvertedInteractionDefinitionId;
                    }
                }
            }

            if (rule.RuleKind == BiologicalInteractionRuleKind.Absorption && string.IsNullOrWhiteSpace(rule.Explanation))
            {
                report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' absorption rule '{rule.EntryId}' must describe its alpha outcome in the explanation field.");
            }
        }

        private void ValidateConversionGraph(IReadOnlyDictionary<string, string> conversionsByEntry, DefinitionValidationReport report)
        {
            const int MaximumConversionDepth = 4;
            foreach (KeyValuePair<string, string> conversion in conversionsByEntry)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { conversion.Key };
                string current = conversion.Value;
                int depth = 0;
                while (!string.IsNullOrWhiteSpace(current) && conversionsByEntry.TryGetValue(current, out string next))
                {
                    depth++;
                    if (depth > MaximumConversionDepth || !seen.Add(current))
                    {
                        report.AddError($"BiologicalCompatibilityProfileDefinition '{DisplayName}' has an unsafe conversion chain starting at '{conversion.Key}'.");
                        break;
                    }

                    current = next;
                }
            }
        }

        private static void ValidateCapabilities(IReadOnlyDictionary<string, IGameDefinition> definitionsById, IEnumerable<CapabilityDefinition> capabilities, string entryId, string label, DefinitionValidationReport report)
        {
            foreach (CapabilityDefinition capability in capabilities ?? Array.Empty<CapabilityDefinition>())
            {
                if (capability == null || definitionsById == null || !definitionsById.TryGetValue(capability.Id, out IGameDefinition registered) || !ReferenceEquals(registered, capability))
                {
                    report.AddError($"Biological compatibility rule '{entryId}' references a missing {label} Capability '{capability?.Id ?? "<null>"}'.");
                }
            }
        }

        private static void ValidateReference<T>(IReadOnlyDictionary<string, IGameDefinition> definitionsById, T definition, string label, DefinitionValidationReport report) where T : UnityEngine.Object, IGameDefinition
        {
            if (definition == null || definitionsById == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(definition.Id, out IGameDefinition registered) || !ReferenceEquals(registered, definition))
            {
                report.AddError($"{label} reference '{definition.Id}' does not resolve for a Biological Compatibility Profile.");
            }
        }

        private static void ValidateCanonicalProfiles(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            BiologicalCompatibilityProfileDefinition first = definitionsById.Values.OfType<BiologicalCompatibilityProfileDefinition>().OrderBy(profile => profile.Id, StringComparer.Ordinal).FirstOrDefault();
            if (first == null)
            {
                return;
            }

            string[] required =
            {
                "compatibility-profile.species.human",
                "compatibility-profile.species.undead-human",
                "compatibility-profile.species.basic-construct",
                "compatibility-profile.species.basic-spirit"
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not BiologicalCompatibilityProfileDefinition)
                {
                    report.AddError($"Canonical BiologicalCompatibilityProfileDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }
    }
}
