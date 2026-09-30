using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology
{
    [CreateAssetMenu(fileName = "Species", menuName = "Unity Isekai Game/Beings/Biology/Species")]
    public sealed class SpeciesDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string speciesId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private BiologicalClassificationDefinition biologicalClassification;
        [SerializeField] private BodyFormDefinition bodyForm;
        [SerializeField] private BiologicalTraitGrantDefinition[] defaultBodyTraits;
        [SerializeField] private BiologicalCapabilityGrantDefinition[] defaultBooleanCapabilities;
        [SerializeField] private BiologicalCapabilityGrantDefinition[] defaultNumericCapabilities;
        [SerializeField] private BiologicalStatContributionDefinition[] calculatedStatContributions;
        [SerializeField] private DefeatPolicyDefinition defaultDefeatPolicy;
        [SerializeField] private AnatomyDefinition anatomyDefinition;
        [SerializeField] private string[] compatibleOriginIds;

        public string Id => speciesId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Being;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public BiologicalClassificationDefinition BiologicalClassification => biologicalClassification;
        public BodyFormDefinition BodyForm => bodyForm;
        public IReadOnlyList<BiologicalTraitGrantDefinition> DefaultBodyTraits => defaultBodyTraits ?? Array.Empty<BiologicalTraitGrantDefinition>();
        public IReadOnlyList<BiologicalCapabilityGrantDefinition> DefaultBooleanCapabilities => defaultBooleanCapabilities ?? Array.Empty<BiologicalCapabilityGrantDefinition>();
        public IReadOnlyList<BiologicalCapabilityGrantDefinition> DefaultNumericCapabilities => defaultNumericCapabilities ?? Array.Empty<BiologicalCapabilityGrantDefinition>();
        public IReadOnlyList<BiologicalStatContributionDefinition> CalculatedStatContributions => calculatedStatContributions ?? Array.Empty<BiologicalStatContributionDefinition>();
        public DefeatPolicyDefinition DefaultDefeatPolicy => defaultDefeatPolicy == null ? biologicalClassification == null ? null : biologicalClassification.DefaultDefeatPolicy : defaultDefeatPolicy;
        public AnatomyDefinition AnatomyDefinition => anatomyDefinition;
        public IReadOnlyList<string> CompatibleOriginIds => compatibleOriginIds ?? Array.Empty<string>();

        private void OnValidate()
        {
            speciesId = speciesId?.Trim();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Species '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("species.", StringComparison.Ordinal))
            {
                report.AddWarning($"Species '{Id}' should use the 'species.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"Species '{Id}' is missing a display name.");
            }

            ValidateDefinitionReference(definitionsById, biologicalClassification, "biological classification", typeof(BiologicalClassificationDefinition), report);
            ValidateDefinitionReference(definitionsById, bodyForm, "body form", typeof(BodyFormDefinition), report);
            ValidateDefinitionReference(definitionsById, anatomyDefinition, "anatomy definition", typeof(AnatomyDefinition), report);
            ValidateGrants(definitionsById, report);

            if (anatomyDefinition != null && !anatomyDefinition.IsCompatibleWith(bodyForm, this))
            {
                report.AddError($"Species '{DisplayName}' references Anatomy '{anatomyDefinition.Id}' that is incompatible with body form '{bodyForm?.Id ?? string.Empty}'.");
            }

            HashSet<string> contributionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (BiologicalStatContributionDefinition contribution in CalculatedStatContributions.Where(contribution => contribution != null && contribution.AlphaEnabled))
            {
                string key = $"{contribution.CalculatedStat?.Id}:{contribution.Kind}:{contribution.Direction}:{contribution.Priority}:{contribution.ContributionId}";
                if (!contributionKeys.Add(key))
                {
                    report.AddError($"Species '{DisplayName}' has duplicate stat contribution source key '{key}'.");
                }
            }

            if (bodyForm != null && biologicalClassification != null && biologicalClassification.OrdinarilyBiological && !bodyForm.PhysicalBody && biologicalClassification.OrdinarilyHasBlood)
            {
                report.AddError($"Species '{DisplayName}' combines blood metadata with a non-physical body form.");
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null)
            {
                return;
            }

            string[] requiredIds =
            {
                "biology.classification.living",
                "biology.classification.undead",
                "biology.classification.construct",
                "biology.classification.spirit",
                "body-form.humanoid",
                "body-form.construct",
                "body-form.incorporeal",
                "anatomy.human",
                "anatomy.basic-construct",
                "anatomy.basic-spirit",
                "species.human",
                "species.undead-human",
                "species.basic-construct",
                "species.basic-spirit"
            };
            if (!requiredIds.Any(id => definitionsById.ContainsKey(id)))
            {
                return;
            }

            SpeciesDefinition firstSpecies = definitionsById.Values.OfType<SpeciesDefinition>().OrderBy(species => species.Id, StringComparer.Ordinal).FirstOrDefault();
            if (!ReferenceEquals(firstSpecies, this))
            {
                return;
            }

            RequireCanonical<BiologicalClassificationDefinition>(definitionsById, "biology.classification.living", "Living biological classification", report);
            RequireCanonical<BiologicalClassificationDefinition>(definitionsById, "biology.classification.undead", "Undead biological classification", report);
            RequireCanonical<BiologicalClassificationDefinition>(definitionsById, "biology.classification.construct", "Construct biological classification", report);
            RequireCanonical<BiologicalClassificationDefinition>(definitionsById, "biology.classification.spirit", "Spirit biological classification", report);
            RequireCanonical<BodyFormDefinition>(definitionsById, "body-form.humanoid", "Humanoid body form", report);
            RequireCanonical<BodyFormDefinition>(definitionsById, "body-form.construct", "Construct body form", report);
            RequireCanonical<BodyFormDefinition>(definitionsById, "body-form.incorporeal", "Incorporeal body form", report);
            RequireCanonical<AnatomyDefinition>(definitionsById, "anatomy.human", "Human Anatomy", report);
            RequireCanonical<AnatomyDefinition>(definitionsById, "anatomy.basic-construct", "Basic Construct Anatomy", report);
            RequireCanonical<AnatomyDefinition>(definitionsById, "anatomy.basic-spirit", "Basic Spirit Anatomy", report);
            RequireCanonical<SpeciesDefinition>(definitionsById, "species.human", "Human Species", report);
            RequireCanonical<SpeciesDefinition>(definitionsById, "species.undead-human", "Undead Human Species", report);
            RequireCanonical<SpeciesDefinition>(definitionsById, "species.basic-construct", "Basic Construct Species", report);
            RequireCanonical<SpeciesDefinition>(definitionsById, "species.basic-spirit", "Basic Spirit Species", report);
        }

        private static void RequireCanonical<TDefinition>(IReadOnlyDictionary<string, IGameDefinition> definitionsById, string id, string label, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not TDefinition)
            {
                report.AddError($"{label} '{id}' must be registered in the canonical alpha definition catalog.");
            }
        }

        private void ValidateDefinitionReference(IReadOnlyDictionary<string, IGameDefinition> definitionsById, ScriptableObject reference, string label, Type expectedType, DefinitionValidationReport report)
        {
            if (reference == null)
            {
                report.AddError($"Species '{DisplayName}' is missing a {label} reference.");
                return;
            }

            if (reference is not IGameDefinition definition)
            {
                report.AddError($"Species '{DisplayName}' {label} reference is not a game definition.");
                return;
            }

            if (definitionsById != null && (!definitionsById.TryGetValue(definition.Id, out IGameDefinition found) || !expectedType.IsInstanceOfType(found)))
            {
                report.AddError($"Species '{DisplayName}' references {label} '{definition.Id}', which is not in the configured catalog.");
            }
        }

        private void ValidateGrants(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (BiologicalTraitGrantDefinition grant in DefaultBodyTraits.Where(grant => grant != null && grant.AlphaEnabled))
            {
                if (grant.Trait == null)
                {
                    report.AddError($"Species '{DisplayName}' has a missing Trait grant.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(grant.Trait.Id, out IGameDefinition found) || found is not Traits.TraitDefinition))
                {
                    report.AddError($"Species '{DisplayName}' references Trait '{grant.Trait.Id}', which is not in the configured catalog.");
                }
            }

            foreach (BiologicalCapabilityGrantDefinition grant in DefaultBooleanCapabilities.Concat(DefaultNumericCapabilities).Where(grant => grant != null && grant.AlphaEnabled))
            {
                if (grant.Capability == null)
                {
                    report.AddError($"Species '{DisplayName}' has a missing Capability grant.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(grant.Capability.Id, out IGameDefinition found) || found is not Capabilities.CapabilityDefinition))
                {
                    report.AddError($"Species '{DisplayName}' references Capability '{grant.Capability.Id}', which is not in the configured catalog.");
                }
            }

            foreach (BiologicalStatContributionDefinition contribution in CalculatedStatContributions.Where(contribution => contribution != null && contribution.AlphaEnabled))
            {
                if (contribution.CalculatedStat == null)
                {
                    report.AddError($"Species '{DisplayName}' has a missing Calculated Stat contribution.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(contribution.CalculatedStat.Id, out IGameDefinition found) || found is not Stats.CalculatedStatDefinition))
                {
                    report.AddError($"Species '{DisplayName}' references Calculated Stat '{contribution.CalculatedStat.Id}', which is not in the configured catalog.");
                }
            }
        }

    }
}
