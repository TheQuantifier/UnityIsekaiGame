using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Combat
{
    [CreateAssetMenu(fileName = "DamageTypeDefinition", menuName = "Unity Isekai Game/Combat/Damage Type")]
    public sealed class DamageTypeDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string damageTypeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private DamageTypeDefinition parentDamageType;
        [SerializeField] private DamageFamily family = DamageFamily.Physical;
        [SerializeField] private bool generalDefenseApplies = true;
        [SerializeField] private bool enforceMinimumDamage = true;
        [SerializeField, Min(0f)] private float minimumDamage = DamageCalculator.DefaultMinimumDamage;
        [SerializeField] private bool trueDamage;
        [SerializeField] private bool canonicalAlphaDamageType;
        [SerializeField] private string resistanceCapabilityId;
        [SerializeField] private string immunityCapabilityId;
        [SerializeField, TextArea(1, 3)] private string presentationNotes;

        public string DamageTypeId => damageTypeId;
        public string Id => damageTypeId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Damage;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public DamageTypeDefinition ParentDamageType => parentDamageType;
        public DamageFamily Family => family;
        public bool GeneralDefenseApplies => generalDefenseApplies;
        public bool EnforceMinimumDamage => enforceMinimumDamage;
        public float MinimumDamage => enforceMinimumDamage ? Mathf.Max(0f, minimumDamage) : 0f;
        public bool IsTrueDamage => trueDamage;
        public bool CanonicalAlphaDamageType => canonicalAlphaDamageType;
        public string ResistanceCapabilityId => string.IsNullOrWhiteSpace(resistanceCapabilityId) ? BuildCapabilityId("resistance") : resistanceCapabilityId;
        public string ImmunityCapabilityId => string.IsNullOrWhiteSpace(immunityCapabilityId) ? BuildCapabilityId("immunity") : immunityCapabilityId;
        public string PresentationNotes => presentationNotes;

        private void OnValidate()
        {
            damageTypeId = damageTypeId?.Trim();
            resistanceCapabilityId = resistanceCapabilityId?.Trim();
            immunityCapabilityId = immunityCapabilityId?.Trim();
            minimumDamage = Mathf.Max(0f, minimumDamage);
            if (trueDamage)
            {
                generalDefenseApplies = false;
                enforceMinimumDamage = false;
                minimumDamage = 0f;
                family = DamageFamily.True;
            }
        }

        public bool IsOrInheritsFrom(DamageTypeDefinition ancestor)
        {
            if (ancestor == null)
            {
                return false;
            }

            HashSet<string> visited = new HashSet<string>();
            DamageTypeDefinition current = this;
            while (current != null && visited.Add(current.Id))
            {
                if (ReferenceEquals(current, ancestor) || current.Id == ancestor.Id)
                {
                    return true;
                }

                current = current.ParentDamageType;
            }

            return false;
        }

        public IEnumerable<DamageTypeDefinition> EnumerateSelfAndAncestors()
        {
            HashSet<string> visited = new HashSet<string>();
            DamageTypeDefinition current = this;
            while (current != null && visited.Add(current.Id))
            {
                yield return current;
                current = current.ParentDamageType;
            }
        }

        public bool HasCircularParentChain()
        {
            HashSet<string> visited = new HashSet<string>();
            DamageTypeDefinition current = this;
            while (current != null)
            {
                if (!visited.Add(current.Id))
                {
                    return true;
                }

                current = current.ParentDamageType;
            }

            return false;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("damage."))
            {
                report.AddWarning($"DamageTypeDefinition '{DisplayName}' should use the 'damage.' namespace prefix.");
            }

            if (primaryCategory == null)
            {
                report.AddWarning($"DamageTypeDefinition '{DisplayName}' has no damage category.");
            }

            if (!string.IsNullOrWhiteSpace(ResistanceCapabilityId) && !ResistanceCapabilityId.StartsWith("capability.damage-resistance.", System.StringComparison.Ordinal))
            {
                report.AddWarning($"DamageTypeDefinition '{DisplayName}' resistance Capability key should use 'capability.damage-resistance.'.");
            }

            if (!string.IsNullOrWhiteSpace(ImmunityCapabilityId) && !ImmunityCapabilityId.StartsWith("capability.damage-immunity.", System.StringComparison.Ordinal))
            {
                report.AddWarning($"DamageTypeDefinition '{DisplayName}' immunity Capability key should use 'capability.damage-immunity.'.");
            }

            if (trueDamage && (generalDefenseApplies || enforceMinimumDamage || minimumDamage > 0f || family != DamageFamily.True))
            {
                report.AddError($"DamageTypeDefinition '{Id}' is True damage but still has mitigation/minimum settings enabled.");
            }

            if (parentDamageType == null)
            {
                return;
            }

            if (ReferenceEquals(this, parentDamageType) || Id == parentDamageType.Id)
            {
                report.AddError($"DamageTypeDefinition '{Id}' cannot be its own parent.");
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(parentDamageType.Id, out IGameDefinition parentDefinition)
                || parentDefinition is not DamageTypeDefinition)
            {
                report.AddError($"DamageTypeDefinition '{Id}' references parent damage type '{parentDamageType.Id}', which is not in the configured catalog.");
            }

            if (HasCircularParentChain())
            {
                report.AddError($"DamageTypeDefinition '{Id}' has a circular parent hierarchy.");
            }
        }

        private string BuildCapabilityId(string kind)
        {
            if (string.IsNullOrWhiteSpace(damageTypeId))
            {
                return string.Empty;
            }

            string suffix = damageTypeId.StartsWith("damage.", System.StringComparison.Ordinal)
                ? damageTypeId.Substring("damage.".Length)
                : damageTypeId;
            suffix = suffix.Replace('.', '-');
            return $"capability.damage-{kind}.{suffix}";
        }
    }
}
