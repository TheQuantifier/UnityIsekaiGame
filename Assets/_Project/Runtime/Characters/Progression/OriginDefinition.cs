using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "OriginDefinition", menuName = "Unity Isekai Game/Progression/Origin Definition")]
    public sealed class OriginDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string originId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private OriginFamilyDefinition family;
        [SerializeField, Min(0f)] private float selectionWeight = 1f;
        [SerializeField] private bool enabledForAlpha = true;
        [SerializeField] private PermanentAttributeGrantDefinition[] startingAttributeGrants;
        [SerializeField] private SkillGrantDefinition[] startingSkillGrants;
        [SerializeField] private BirthGiftDefinition[] influencedGiftPool;
        [SerializeField] private BirthGiftWeightModifierDefinition[] giftWeightModifiers;
        [SerializeField] private RarityWeightModifierDefinition[] giftRarityWeightModifiers;
        [SerializeField] private ProgressionCurrencyGrantDefinition startingGold;
        [SerializeField] private RoleDefinition startingRole;
        [SerializeField] private SocialStatusAssignmentDefinition[] startingSocialStatuses;
        [SerializeField] private TitleDefinition startingTitle;
        [SerializeField] private string futureHomeReference;
        [SerializeField] private string futureRelationshipReferences;
        [SerializeField] private string futureFactionObligations;
        [SerializeField] private string futureStartingLocationReference;

        public string OriginId => originId;
        public string Id => originId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Origin;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public OriginFamilyDefinition Family => family;
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);
        public bool EnabledForAlpha => enabledForAlpha;
        public IReadOnlyList<PermanentAttributeGrantDefinition> StartingAttributeGrants => startingAttributeGrants ?? System.Array.Empty<PermanentAttributeGrantDefinition>();
        public IReadOnlyList<SkillGrantDefinition> StartingSkillGrants => startingSkillGrants ?? System.Array.Empty<SkillGrantDefinition>();
        public IReadOnlyList<BirthGiftDefinition> InfluencedGiftPool => influencedGiftPool ?? System.Array.Empty<BirthGiftDefinition>();
        public IReadOnlyList<BirthGiftWeightModifierDefinition> GiftWeightModifiers => giftWeightModifiers ?? System.Array.Empty<BirthGiftWeightModifierDefinition>();
        public IReadOnlyList<RarityWeightModifierDefinition> GiftRarityWeightModifiers => giftRarityWeightModifiers ?? System.Array.Empty<RarityWeightModifierDefinition>();
        public ProgressionCurrencyGrantDefinition StartingGold => startingGold;
        public RoleDefinition StartingRole => startingRole;
        public IReadOnlyList<SocialStatusAssignmentDefinition> StartingSocialStatuses => startingSocialStatuses ?? System.Array.Empty<SocialStatusAssignmentDefinition>();
        public TitleDefinition StartingTitle => startingTitle;

        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0f, selectionWeight);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("origin."))
            {
                report.AddWarning($"Origin '{DisplayName}' should use the 'origin.' namespace prefix.");
            }

            if (family == null)
            {
                report.AddError($"Origin '{DisplayName}' is missing an origin family.");
            }
            else if (definitionsById == null || !definitionsById.TryGetValue(family.Id, out IGameDefinition foundFamily) || foundFamily is not OriginFamilyDefinition)
            {
                report.AddError($"Origin '{DisplayName}' references family '{family.Id}', which is not in the configured catalog.");
            }

            if (enabledForAlpha && selectionWeight <= 0f)
            {
                report.AddError($"Origin '{DisplayName}' is alpha-enabled but has no selection weight.");
            }

            foreach (PermanentAttributeGrantDefinition grant in StartingAttributeGrants)
            {
                if (grant == null || !grant.IsValid)
                {
                    report.AddError($"Origin '{DisplayName}' has an invalid starting attribute grant.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(grant.Attribute.Id, out IGameDefinition registeredAttribute) || !ReferenceEquals(registeredAttribute, grant.Attribute))
                {
                    report.AddError($"Origin '{DisplayName}' references attribute '{grant.Attribute.Id}', which is not in the configured catalog.");
                }

                if (grant.Amount > 5f)
                {
                    report.AddWarning($"Origin '{DisplayName}' grants {grant.Amount:0.##} {grant.Attribute.DisplayName}; alpha origin grants should stay small.");
                }
            }

            foreach (SkillGrantDefinition grant in StartingSkillGrants)
            {
                if (grant == null || grant.Skill == null)
                {
                    report.AddError($"Origin '{DisplayName}' has a missing starting Skill grant.");
                    continue;
                }

                ValidateDefinitionReference(grant.Skill, nameof(SkillDefinition), definitionsById, report, $"Origin '{DisplayName}' starting Skill");
            }

            if (startingGold != null)
            {
                ValidateCurrencyGrant("starting Gold", startingGold, definitionsById, report);
            }

            if (enabledForAlpha && startingRole == null)
            {
                report.AddError($"Origin '{DisplayName}' is missing a starting role.");
            }
            else if (startingRole != null)
            {
                ValidateDefinitionReference(startingRole, nameof(RoleDefinition), definitionsById, report, $"Origin '{DisplayName}' starting role");
            }

            HashSet<string> statusKeys = new HashSet<string>();
            foreach (SocialStatusAssignmentDefinition assignment in StartingSocialStatuses)
            {
                if (assignment == null || assignment.SocialStatus == null)
                {
                    report.AddError($"Origin '{DisplayName}' has a missing starting social status.");
                    continue;
                }

                ValidateDefinitionReference(assignment.SocialStatus, nameof(SocialStatusDefinition), definitionsById, report, $"Origin '{DisplayName}' starting social status");
                string key = $"{assignment.SocialStatus.Id}|{assignment.ContextKind}|{assignment.ResolveContextTargetId()}";
                if (!statusKeys.Add(key))
                {
                    report.AddError($"Origin '{DisplayName}' has duplicate starting social status/context '{key}'.");
                }
            }

            if (startingTitle != null)
            {
                ValidateDefinitionReference(startingTitle, nameof(TitleDefinition), definitionsById, report, $"Origin '{DisplayName}' starting title");
            }

            foreach (BirthGiftDefinition gift in InfluencedGiftPool)
            {
                if (gift == null)
                {
                    report.AddError($"Origin '{DisplayName}' has a missing influenced gift reference.");
                    continue;
                }

                ValidateDefinitionReference(gift, nameof(BirthGiftDefinition), definitionsById, report, $"Origin '{DisplayName}' influenced gift");
            }
        }

        private static void ValidateCurrencyGrant(string label, ProgressionCurrencyGrantDefinition grant, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (grant.Currency == null)
            {
                report.AddError($"Origin {label} is missing a currency.");
                return;
            }

            ValidateDefinitionReference(grant.Currency, nameof(CurrencyDefinition), definitionsById, report, $"Origin {label}");
        }

        private static void ValidateDefinitionReference(IGameDefinition definition, string expectedType, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, string label)
        {
            if (definition == null)
            {
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(definition.Id, out IGameDefinition found) || found.GetType().Name != expectedType)
            {
                report.AddError($"{label} references '{definition.Id}', which is not a configured {expectedType}.");
            }
        }
    }
}
