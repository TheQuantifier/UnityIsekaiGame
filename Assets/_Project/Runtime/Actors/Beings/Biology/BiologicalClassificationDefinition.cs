using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology
{
    [CreateAssetMenu(fileName = "BiologicalClassification", menuName = "Unity Isekai Game/Beings/Biology/Biological Classification")]
    public sealed class BiologicalClassificationDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string classificationId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private bool ordinarilyLiving;
        [SerializeField] private bool ordinarilyBiological = true;
        [SerializeField] private bool ordinarilyRequiresBreathing;
        [SerializeField] private bool ordinarilyHasBlood;
        [SerializeField] private bool ordinarilyCanBecomeUnconscious = true;
        [SerializeField] private bool ordinarilyCanDie = true;
        [SerializeField] private bool ordinaryBiologicalHealingCompatible = true;
        [SerializeField] private bool poisonCompatible = true;
        [SerializeField] private bool diseaseCompatible = true;
        [SerializeField] private DefeatPolicyDefinition defaultDefeatPolicy;
        [SerializeField] private BiologicalTraitGrantDefinition[] defaultTraitGrants;
        [SerializeField] private BiologicalCapabilityGrantDefinition[] defaultCapabilityGrants;
        [SerializeField] private BiologicalStatContributionDefinition[] defaultStatContributions;

        public string Id => classificationId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Being;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public bool OrdinarilyLiving => ordinarilyLiving;
        public bool OrdinarilyBiological => ordinarilyBiological;
        public bool OrdinarilyRequiresBreathing => ordinarilyRequiresBreathing;
        public bool OrdinarilyHasBlood => ordinarilyHasBlood;
        public bool OrdinarilyCanBecomeUnconscious => ordinarilyCanBecomeUnconscious;
        public bool OrdinarilyCanDie => ordinarilyCanDie;
        public bool OrdinaryBiologicalHealingCompatible => ordinaryBiologicalHealingCompatible;
        public bool PoisonCompatible => poisonCompatible;
        public bool DiseaseCompatible => diseaseCompatible;
        public DefeatPolicyDefinition DefaultDefeatPolicy => defaultDefeatPolicy;
        public IReadOnlyList<BiologicalTraitGrantDefinition> DefaultTraitGrants => defaultTraitGrants ?? Array.Empty<BiologicalTraitGrantDefinition>();
        public IReadOnlyList<BiologicalCapabilityGrantDefinition> DefaultCapabilityGrants => defaultCapabilityGrants ?? Array.Empty<BiologicalCapabilityGrantDefinition>();
        public IReadOnlyList<BiologicalStatContributionDefinition> DefaultStatContributions => defaultStatContributions ?? Array.Empty<BiologicalStatContributionDefinition>();

        private void OnValidate()
        {
            classificationId = classificationId?.Trim();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Biological classification '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("biology.classification.", StringComparison.Ordinal))
            {
                report.AddWarning($"Biological classification '{Id}' should use the 'biology.classification.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"Biological classification '{Id}' is missing a display name.");
            }

            if (!ordinarilyLiving && (ordinarilyRequiresBreathing || ordinarilyHasBlood) && !ordinarilyBiological)
            {
                report.AddError($"Biological classification '{DisplayName}' has non-biological metadata but ordinary blood/breathing flags.");
            }

            ValidateGrants(definitionsById, report);
        }

        private void ValidateGrants(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (BiologicalCapabilityGrantDefinition grant in DefaultCapabilityGrants.Where(grant => grant != null && grant.AlphaEnabled))
            {
                if (grant.Capability == null)
                {
                    report.AddError($"Biological classification '{DisplayName}' has a missing Capability grant.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(grant.Capability.Id, out IGameDefinition found) || found is not Capabilities.CapabilityDefinition))
                {
                    report.AddError($"Biological classification '{DisplayName}' references Capability '{grant.Capability.Id}' outside the configured catalog.");
                }
            }

            foreach (BiologicalTraitGrantDefinition grant in DefaultTraitGrants.Where(grant => grant != null && grant.AlphaEnabled))
            {
                if (grant.Trait == null)
                {
                    report.AddError($"Biological classification '{DisplayName}' has a missing Trait grant.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(grant.Trait.Id, out IGameDefinition found) || found is not Traits.TraitDefinition))
                {
                    report.AddError($"Biological classification '{DisplayName}' references Trait '{grant.Trait.Id}' outside the configured catalog.");
                }
            }
        }

    }
}
