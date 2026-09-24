using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Traits
{
    [CreateAssetMenu(fileName = "TraitDefinition", menuName = "Unity Isekai Game/Traits/Trait Definition")]
    public sealed class TraitDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string traitId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private TraitPolarity polarity = TraitPolarity.Neutral;
        [SerializeField] private TraitPermanenceClass permanenceClass = TraitPermanenceClass.AcquiredPermanent;
        [SerializeField] private TraitLifecycleState defaultLifecycle = TraitLifecycleState.Active;
        [SerializeField] private TraitDiscoveryState defaultDiscovery = TraitDiscoveryState.Discovered;
        [SerializeField] private TraitVisibility defaultVisibility = TraitVisibility.Known;
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField] private string[] conflictGroupIds;
        [SerializeField] private TraitDefinition[] incompatibleTraits;
        [SerializeField] private TraitCalculatedStatContributionDefinition[] calculatedStatContributions;
        [SerializeField] private TraitAbilityGrantDefinition[] abilityGrants;
        [SerializeField] private TraitCapabilityGrantDefinition[] booleanCapabilityGrants;
        [SerializeField] private TraitCapabilityGrantDefinition[] numericCapabilityGrants;
        [SerializeField] private TraitResistanceGrantDefinition[] resistanceGrants;
        [SerializeField] private TraitResistanceGrantDefinition[] immunityGrants;
        [SerializeField] private TraitLinkedGrantDefinition[] linkedTraitGrants;
        [SerializeField] private TraitSkillGrantDefinition[] skillGrants;
        [SerializeField] private RequirementSetDefinition acquisitionRequirements;
        [SerializeField] private RequirementSetDefinition activationRequirements;
        [SerializeField] private string upgradeReplacementMetadata;

        public string Id => traitId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Trait;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public TraitPolarity Polarity => polarity;
        public TraitPermanenceClass PermanenceClass => permanenceClass;
        public TraitLifecycleState DefaultLifecycle => defaultLifecycle;
        public TraitDiscoveryState DefaultDiscovery => defaultDiscovery;
        public TraitVisibility DefaultVisibility => defaultVisibility;
        public bool AlphaEnabled => alphaEnabled;
        public IReadOnlyList<string> ConflictGroupIds => conflictGroupIds ?? Array.Empty<string>();
        public IReadOnlyList<TraitDefinition> IncompatibleTraits => incompatibleTraits ?? Array.Empty<TraitDefinition>();
        public IReadOnlyList<TraitCalculatedStatContributionDefinition> CalculatedStatContributions => calculatedStatContributions ?? Array.Empty<TraitCalculatedStatContributionDefinition>();
        public IReadOnlyList<TraitAbilityGrantDefinition> AbilityGrants => abilityGrants ?? Array.Empty<TraitAbilityGrantDefinition>();
        public IReadOnlyList<TraitCapabilityGrantDefinition> BooleanCapabilityGrants => booleanCapabilityGrants ?? Array.Empty<TraitCapabilityGrantDefinition>();
        public IReadOnlyList<TraitCapabilityGrantDefinition> NumericCapabilityGrants => numericCapabilityGrants ?? Array.Empty<TraitCapabilityGrantDefinition>();
        public IReadOnlyList<TraitResistanceGrantDefinition> ResistanceGrants => resistanceGrants ?? Array.Empty<TraitResistanceGrantDefinition>();
        public IReadOnlyList<TraitResistanceGrantDefinition> ImmunityGrants => immunityGrants ?? Array.Empty<TraitResistanceGrantDefinition>();
        public IReadOnlyList<TraitLinkedGrantDefinition> LinkedTraitGrants => linkedTraitGrants ?? Array.Empty<TraitLinkedGrantDefinition>();
        public IReadOnlyList<TraitSkillGrantDefinition> SkillGrants => skillGrants ?? Array.Empty<TraitSkillGrantDefinition>();
        public RequirementSetDefinition AcquisitionRequirements => acquisitionRequirements;
        public RequirementSetDefinition ActivationRequirements => activationRequirements;
        public string UpgradeReplacementMetadata => upgradeReplacementMetadata ?? string.Empty;

        private void OnValidate()
        {
            traitId = traitId?.Trim();
            conflictGroupIds = conflictGroupIds == null
                ? Array.Empty<string>()
                : conflictGroupIds.Select(value => value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Trait '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("trait.", StringComparison.Ordinal))
            {
                report.AddWarning($"Trait '{Id}' should use the 'trait.' namespace prefix.");
            }

            ValidateEnum(polarity, nameof(TraitPolarity), report);
            ValidateEnum(permanenceClass, nameof(TraitPermanenceClass), report);
            ValidateEnum(defaultLifecycle, nameof(TraitLifecycleState), report);
            ValidateEnum(defaultDiscovery, nameof(TraitDiscoveryState), report);
            ValidateEnum(defaultVisibility, nameof(TraitVisibility), report);

            HashSet<string> groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (string group in ConflictGroupIds)
            {
                if (!groups.Add(group))
                {
                    report.AddError($"Trait '{DisplayName}' has duplicate conflict group '{group}'.");
                }
            }

            HashSet<string> incompatible = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraitDefinition trait in IncompatibleTraits)
            {
                if (trait == null)
                {
                    report.AddError($"Trait '{DisplayName}' has a missing incompatible Trait reference.");
                    continue;
                }

                if (string.Equals(trait.Id, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Trait '{DisplayName}' cannot be incompatible with itself.");
                }

                if (!incompatible.Add(trait.Id))
                {
                    report.AddError($"Trait '{DisplayName}' has duplicate incompatible Trait '{trait.Id}'.");
                }

                ValidateDefinitionReference<TraitDefinition>(trait, definitionsById, report, $"Trait '{DisplayName}' incompatible Trait");
            }

            foreach (TraitLinkedGrantDefinition linked in LinkedTraitGrants)
            {
                if (linked == null || linked.Trait == null)
                {
                    report.AddError($"Trait '{DisplayName}' has a missing linked Trait grant.");
                    continue;
                }

                if (string.Equals(linked.Trait.Id, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Trait '{DisplayName}' directly links to itself.");
                }

                ValidateDefinitionReference<TraitDefinition>(linked.Trait, definitionsById, report, $"Trait '{DisplayName}' linked Trait");
            }

            ValidateLinkedCycles(definitionsById, report);
            ValidateContributions(definitionsById, report);
            ValidateCapabilityGrants(BooleanCapabilityGrants, CapabilityValueType.Boolean, definitionsById, report);
            ValidateCapabilityGrants(NumericCapabilityGrants, CapabilityValueType.Numeric, definitionsById, report);
            ValidateAbilityGrants(definitionsById, report);
            ValidateSkillGrants(definitionsById, report);
            ValidateRequirement(acquisitionRequirements, definitionsById, report, "acquisition");
            ValidateRequirement(activationRequirements, definitionsById, report, "activation");
        }

        private void ValidateContributions(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> entries = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraitCalculatedStatContributionDefinition contribution in CalculatedStatContributions)
            {
                if (contribution == null || contribution.CalculatedStat == null)
                {
                    report.AddError($"Trait '{DisplayName}' has a missing Calculated Stat contribution.");
                    continue;
                }

                if (!entries.Add(contribution.EntryId))
                {
                    report.AddError($"Trait '{DisplayName}' has duplicate contribution entry '{contribution.EntryId}'.");
                }

                ValidateDefinitionReference<CalculatedStatDefinition>(contribution.CalculatedStat, definitionsById, report, $"Trait '{DisplayName}' Calculated Stat contribution");
                if (contribution.Magnitude < 0f || float.IsNaN(contribution.Magnitude) || float.IsInfinity(contribution.Magnitude))
                {
                    report.AddError($"Trait '{DisplayName}' has an invalid contribution magnitude.");
                }
            }
        }

        private void ValidateCapabilityGrants(IReadOnlyList<TraitCapabilityGrantDefinition> grants, CapabilityValueType expected, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> entries = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraitCapabilityGrantDefinition grant in grants)
            {
                if (grant == null || grant.Capability == null)
                {
                    report.AddError($"Trait '{DisplayName}' has a missing {expected} Capability grant.");
                    continue;
                }

                if (!entries.Add(grant.EntryId))
                {
                    report.AddError($"Trait '{DisplayName}' has duplicate Capability grant entry '{grant.EntryId}'.");
                }

                ValidateDefinitionReference<CapabilityDefinition>(grant.Capability, definitionsById, report, $"Trait '{DisplayName}' Capability grant");
                if (grant.Capability.ValueType != expected)
                {
                    report.AddError($"Trait '{DisplayName}' grants Capability '{grant.Capability.Id}' as {expected}, but the definition is {grant.Capability.ValueType}.");
                }
            }
        }

        private void ValidateAbilityGrants(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (TraitAbilityGrantDefinition grant in AbilityGrants)
            {
                if (grant == null || string.IsNullOrWhiteSpace(grant.AbilityId))
                {
                    report.AddError($"Trait '{DisplayName}' has a missing ability/action grant.");
                    continue;
                }

                if (grant.Ability != null)
                {
                    ValidateDefinitionReference<AbilityDefinition>(grant.Ability, definitionsById, report, $"Trait '{DisplayName}' ability grant");
                }
            }
        }

        private void ValidateSkillGrants(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (TraitSkillGrantDefinition grant in SkillGrants)
            {
                if (grant == null || grant.Skill == null)
                {
                    report.AddError($"Trait '{DisplayName}' has a missing Skill grant.");
                    continue;
                }

                ValidateDefinitionReference<SkillDefinition>(grant.Skill, definitionsById, report, $"Trait '{DisplayName}' Skill grant");
            }
        }

        private void ValidateRequirement(RequirementSetDefinition requirement, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, string label)
        {
            if (requirement == null)
            {
                return;
            }

            ValidateDefinitionReference<RequirementSetDefinition>(requirement, definitionsById, report, $"Trait '{DisplayName}' {label} requirement");
        }

        private void ValidateLinkedCycles(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || string.IsNullOrWhiteSpace(Id))
            {
                return;
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> stack = new HashSet<string>(StringComparer.Ordinal);
            if (HasCycle(this, definitionsById, visited, stack))
            {
                report.AddError($"Trait '{DisplayName}' participates in a linked Trait cycle.");
            }
        }

        private static bool HasCycle(TraitDefinition trait, IReadOnlyDictionary<string, IGameDefinition> definitionsById, HashSet<string> visited, HashSet<string> stack)
        {
            if (trait == null || string.IsNullOrWhiteSpace(trait.Id))
            {
                return false;
            }

            if (stack.Contains(trait.Id))
            {
                return true;
            }

            if (!visited.Add(trait.Id))
            {
                return false;
            }

            stack.Add(trait.Id);
            foreach (TraitLinkedGrantDefinition linked in trait.LinkedTraitGrants)
            {
                TraitDefinition linkedTrait = linked?.Trait;
                if (linkedTrait != null && definitionsById.TryGetValue(linkedTrait.Id, out IGameDefinition found) && found is TraitDefinition resolved)
                {
                    if (HasCycle(resolved, definitionsById, visited, stack))
                    {
                        return true;
                    }
                }
            }

            stack.Remove(trait.Id);
            return false;
        }

        private void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Trait '{DisplayName}' has an invalid {enumName} value.");
            }
        }

        private static void ValidateDefinitionReference<TDefinition>(IGameDefinition definition, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, string label)
            where TDefinition : class, IGameDefinition
        {
            if (definition == null || definitionsById == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(definition.Id, out IGameDefinition found) || found is not TDefinition)
            {
                report.AddError($"{label} references '{definition.Id}', which is not a configured {typeof(TDefinition).Name}.");
            }
        }
    }
}
