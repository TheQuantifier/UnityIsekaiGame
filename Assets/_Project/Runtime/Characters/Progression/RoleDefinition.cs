using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "RoleDefinition", menuName = "Unity Isekai Game/Progression/Role Definition")]
    public sealed class RoleDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string roleId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private CalculatedStatModifierDefinition[] calculatedStatModifiers;
        [SerializeField] private ResistanceModifierDefinition[] resistanceModifiers;
        [SerializeField] private ProgressionAbilityReference[] grantedAbilities;
        [SerializeField] private ProgressionAbilityReference[] blockedAbilities;
        [SerializeField] private ProgressionPolicyPayload[] permissions;
        [SerializeField] private ProgressionPolicyPayload[] duties;
        [SerializeField] private ProgressionPolicyPayload[] factionRelationshipReferences;
        [SerializeField] private ProgressionPolicyPayload[] rulerPersonRelationshipReferences;
        [SerializeField] private ProgressionPolicyPayload[] employerInstitutionReferences;
        [SerializeField] private ProgressionPolicyPayload periodicPaySupportConfiguration;
        [SerializeField] private ProgressionPolicyPayload[] legalAuthority;
        [SerializeField] private ProgressionPolicyPayload[] locationAccess;
        [SerializeField] private ProgressionPolicyPayload[] serviceAccess;
        [SerializeField] private ProgressionPolicyPayload[] requiredEquipmentOrUniformReferences;
        [SerializeField] private RoleDefinition[] incompatibleRoles;
        [SerializeField] private string[] incompatibilityGroups;
        [SerializeField] private string suspensionConditionsMetadata;
        [SerializeField] private string revocationConditionsMetadata;
        [SerializeField] private string futureAbandonmentPolicy;
        [SerializeField] private string futureHiddenConflictPolicy;
        [SerializeField] private string futureDiscoveryConsequences;

        public string RoleId => roleId;
        public string Id => roleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Role;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public IReadOnlyList<CalculatedStatModifierDefinition> CalculatedStatModifiers => calculatedStatModifiers ?? System.Array.Empty<CalculatedStatModifierDefinition>();
        public IReadOnlyList<ResistanceModifierDefinition> ResistanceModifiers => resistanceModifiers ?? System.Array.Empty<ResistanceModifierDefinition>();
        public IReadOnlyList<ProgressionAbilityReference> GrantedAbilities => grantedAbilities ?? System.Array.Empty<ProgressionAbilityReference>();
        public IReadOnlyList<ProgressionAbilityReference> BlockedAbilities => blockedAbilities ?? System.Array.Empty<ProgressionAbilityReference>();
        public IReadOnlyList<RoleDefinition> IncompatibleRoles => incompatibleRoles ?? System.Array.Empty<RoleDefinition>();
        public IReadOnlyList<string> IncompatibilityGroups => incompatibilityGroups ?? System.Array.Empty<string>();

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("role."))
            {
                report.AddWarning($"Role '{DisplayName}' should use the 'role.' namespace prefix.");
            }

            HashSet<string> seenRoles = new HashSet<string>();
            foreach (RoleDefinition role in IncompatibleRoles)
            {
                if (role == null)
                {
                    report.AddError($"Role '{DisplayName}' has a missing incompatible role reference.");
                    continue;
                }

                if (role == this || role.Id == Id)
                {
                    report.AddError($"Role '{DisplayName}' cannot be incompatible with itself.");
                }

                if (!seenRoles.Add(role.Id))
                {
                    report.AddError($"Role '{DisplayName}' has duplicate incompatible role '{role.Id}'.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(role.Id, out IGameDefinition found) || found is not RoleDefinition)
                {
                    report.AddError($"Role '{DisplayName}' references incompatible role '{role.Id}', which is not in the configured catalog.");
                }
            }

            HashSet<string> groupKeys = new HashSet<string>();
            foreach (string group in IncompatibilityGroups)
            {
                if (string.IsNullOrWhiteSpace(group))
                {
                    report.AddError($"Role '{DisplayName}' has a blank incompatibility group.");
                    continue;
                }

                if (!groupKeys.Add(group))
                {
                    report.AddError($"Role '{DisplayName}' has duplicate incompatibility group '{group}'.");
                }
            }

            ValidateStatModifiers("Role", DisplayName, CalculatedStatModifiers, definitionsById, report);
            ValidateAbilityReferences("Role", DisplayName, "grant", GrantedAbilities, definitionsById, report);
            ValidateAbilityReferences("Role", DisplayName, "block", BlockedAbilities, definitionsById, report);
            ValidateResistanceModifiers("Role", DisplayName, ResistanceModifiers, definitionsById, report);
        }

        internal static void ValidateStatModifiers(string ownerType, string ownerName, IReadOnlyList<CalculatedStatModifierDefinition> modifiers, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (CalculatedStatModifierDefinition modifier in modifiers)
            {
                if (modifier == null || !modifier.IsValid)
                {
                    report.AddError($"{ownerType} '{ownerName}' has an invalid stat modifier.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(modifier.Stat.Id, out IGameDefinition registeredStat) || !ReferenceEquals(registeredStat, modifier.Stat))
                {
                    report.AddError($"{ownerType} '{ownerName}' references calculated stat '{modifier.Stat.Id}' outside the configured catalog.");
                }

                string key = $"{modifier.Stat.Id}:{modifier.Operation}:{modifier.Priority}";
                if (!keys.Add(key))
                {
                    report.AddError($"{ownerType} '{ownerName}' has duplicate stat modifier key '{key}'.");
                }
            }
        }

        internal static void ValidateResistanceModifiers(string ownerType, string ownerName, IReadOnlyList<ResistanceModifierDefinition> modifiers, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (ResistanceModifierDefinition modifier in modifiers)
            {
                if (modifier == null || modifier.DamageType == null || !modifier.IsValid)
                {
                    report.AddError($"{ownerType} '{ownerName}' has an invalid resistance modifier.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(modifier.DamageType.Id, out IGameDefinition found) || found is not DamageTypeDefinition)
                {
                    report.AddError($"{ownerType} '{ownerName}' references damage type '{modifier.DamageType.Id}', which is not in the configured catalog.");
                }

                string key = $"{modifier.DamageType.Id}:{modifier.Priority}";
                if (!keys.Add(key))
                {
                    report.AddError($"{ownerType} '{ownerName}' has duplicate resistance modifier key '{key}'.");
                }
            }
        }

        internal static void ValidateAbilityReferences(string ownerType, string ownerName, string operation, IReadOnlyList<ProgressionAbilityReference> references, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            HashSet<string> abilityIds = new HashSet<string>();
            foreach (ProgressionAbilityReference abilityReference in references)
            {
                UnityIsekaiGame.Abilities.AbilityDefinition ability = abilityReference?.Ability;
                if (ability == null || !abilityIds.Add(ability.Id) || definitionsById == null || !definitionsById.TryGetValue(ability.Id, out IGameDefinition registeredAbility) || !ReferenceEquals(registeredAbility, ability))
                {
                    report.AddError($"{ownerType} '{ownerName}' has a missing, duplicate, or unregistered ability {operation}.");
                }
            }
        }
    }
}
