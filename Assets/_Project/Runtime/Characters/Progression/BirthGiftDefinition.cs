using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Progression
{
    [CreateAssetMenu(fileName = "BirthGiftDefinition", menuName = "Unity Isekai Game/Progression/Birth Gift Definition")]
    public sealed class BirthGiftDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IHasRarity, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string birthGiftId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private RarityDefinition rarity;
        [SerializeField, Min(0f)] private float selectionWeight = 1f;
        [SerializeField] private BirthGiftType giftType = BirthGiftType.PermanentAttributeGrant;
        [SerializeField] private ProgressionAbilityReference grantedAbility;
        [SerializeField] private SkillGrantDefinition[] skillGrants;
        [SerializeField] private PermanentAttributeGrantDefinition[] permanentAttributeGrants;
        [SerializeField] private PermanentAttributeGrantDefinition[] growthAffinityGrants;
        [SerializeField] private BirthGiftAwakeningMode awakeningMode = BirthGiftAwakeningMode.ImmediateAutomatic;
        [SerializeField, Min(0f)] private float requiredActivePlaytimeSeconds;
        [SerializeField] private bool enabledForAlpha = true;

        public string BirthGiftId => birthGiftId;
        public string Id => birthGiftId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.BirthGift;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public RarityDefinition Rarity => rarity;
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);
        public BirthGiftType GiftType => giftType;
        public ProgressionAbilityReference GrantedAbility => grantedAbility;
        public IReadOnlyList<SkillGrantDefinition> SkillGrants => skillGrants ?? System.Array.Empty<SkillGrantDefinition>();
        public IReadOnlyList<PermanentAttributeGrantDefinition> PermanentAttributeGrants => permanentAttributeGrants ?? System.Array.Empty<PermanentAttributeGrantDefinition>();
        public IReadOnlyList<PermanentAttributeGrantDefinition> GrowthAffinityGrants => growthAffinityGrants ?? System.Array.Empty<PermanentAttributeGrantDefinition>();
        public BirthGiftAwakeningMode AwakeningMode => awakeningMode;
        public float RequiredActivePlaytimeSeconds => Mathf.Max(0f, requiredActivePlaytimeSeconds);
        public bool EnabledForAlpha => enabledForAlpha;

        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0f, selectionWeight);
            requiredActivePlaytimeSeconds = Mathf.Max(0f, requiredActivePlaytimeSeconds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!Id.StartsWith("birth-gift."))
            {
                report.AddWarning($"Birth gift '{DisplayName}' should use the 'birth-gift.' namespace prefix.");
            }

            if (rarity == null)
            {
                report.AddError($"Birth gift '{DisplayName}' is missing a rarity.");
            }

            if (enabledForAlpha && selectionWeight <= 0f)
            {
                report.AddError($"Birth gift '{DisplayName}' is alpha-enabled but has no selection weight.");
            }

            if (!System.Enum.IsDefined(typeof(BirthGiftType), giftType))
            {
                report.AddError($"Birth gift '{DisplayName}' has an invalid gift type.");
            }

            if (!System.Enum.IsDefined(typeof(BirthGiftAwakeningMode), awakeningMode))
            {
                report.AddError($"Birth gift '{DisplayName}' has an invalid awakening mode.");
            }

            if (awakeningMode == BirthGiftAwakeningMode.DelayedActivePlaytime && requiredActivePlaytimeSeconds <= 0f)
            {
                report.AddError($"Birth gift '{DisplayName}' uses delayed active playtime without a delay.");
            }

            if (giftType == BirthGiftType.PermanentAttributeGrant && PermanentAttributeGrants.Count == 0)
            {
                report.AddError($"Birth gift '{DisplayName}' is a permanent attribute grant but has no grants.");
            }

            if (giftType == BirthGiftType.GrowthAffinity && GrowthAffinityGrants.Count == 0)
            {
                report.AddError($"Birth gift '{DisplayName}' is a growth affinity but has no affinity grants.");
            }

            if (giftType == BirthGiftType.LatentSkill)
            {
                if (grantedAbility?.Ability == null)
                {
                    report.AddError($"Birth gift '{DisplayName}' is a latent ability gift but has no ability reference.");
                }
                else if (definitionsById == null || !definitionsById.TryGetValue(grantedAbility.Ability.Id, out IGameDefinition found) || !ReferenceEquals(found, grantedAbility.Ability))
                {
                    report.AddError($"Birth gift '{DisplayName}' references ability '{grantedAbility.Ability.Id}', which is not in the configured catalog.");
                }
            }

            foreach (PermanentAttributeGrantDefinition grant in PermanentAttributeGrants)
            {
                if (grant == null || !grant.IsValid)
                {
                    report.AddError($"Birth gift '{DisplayName}' has an invalid permanent attribute grant.");
                }
                else if (definitionsById == null || !definitionsById.TryGetValue(grant.Attribute.Id, out IGameDefinition registeredAttribute) || !ReferenceEquals(registeredAttribute, grant.Attribute))
                {
                    report.AddError($"Birth gift '{DisplayName}' references attribute '{grant.Attribute.Id}', which is not in the configured catalog.");
                }
            }

            foreach (PermanentAttributeGrantDefinition grant in GrowthAffinityGrants)
            {
                if (grant == null || !grant.IsValid)
                {
                    report.AddError($"Birth gift '{DisplayName}' has an invalid growth affinity grant.");
                }
                else if (definitionsById == null || !definitionsById.TryGetValue(grant.Attribute.Id, out IGameDefinition registeredAttribute) || !ReferenceEquals(registeredAttribute, grant.Attribute))
                {
                    report.AddError($"Birth gift '{DisplayName}' references growth affinity attribute '{grant.Attribute.Id}', which is not in the configured catalog.");
                }
            }

            foreach (SkillGrantDefinition grant in SkillGrants)
            {
                if (grant == null || grant.Skill == null)
                {
                    report.AddError($"Birth gift '{DisplayName}' has a missing Skill grant.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(grant.Skill.Id, out IGameDefinition found) || found is not SkillDefinition)
                {
                    report.AddError($"Birth gift '{DisplayName}' references Skill '{grant.Skill.Id}', which is not in the configured catalog.");
                }
            }
        }
    }
}
