using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Skills
{
    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "Unity Isekai Game/Skills/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SkillType skillTypes;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField] private SkillNaturalLearningDefinition naturalLearning = new SkillNaturalLearningDefinition();
        [SerializeField] private SkillGrade defaultNaturalStartingGrade = SkillGrade.F;
        [SerializeField] private SkillXpThresholdDefinition[] xpThresholds;
        [SerializeField] private SkillGradeEffectPackageDefinition[] gradePackages;
        [SerializeField] private SkillAbilityUnlockDefinition[] abilityUnlocks;
        [SerializeField] private SkillGrade directGrantDefaultGrade = SkillGrade.F;

        public string SkillId => skillId;
        public string Id => skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public SkillType SkillTypes => skillTypes;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Skill;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public bool AlphaEnabled => alphaEnabled;
        public SkillNaturalLearningDefinition NaturalLearning => naturalLearning;
        public SkillGrade DefaultNaturalStartingGrade => SkillGradeUtility.Clamp(defaultNaturalStartingGrade);
        public IReadOnlyList<SkillXpThresholdDefinition> XpThresholds => xpThresholds ?? System.Array.Empty<SkillXpThresholdDefinition>();
        public IReadOnlyList<SkillGradeEffectPackageDefinition> GradePackages => gradePackages ?? System.Array.Empty<SkillGradeEffectPackageDefinition>();
        public IReadOnlyList<SkillAbilityUnlockDefinition> AbilityUnlocks => abilityUnlocks ?? System.Array.Empty<SkillAbilityUnlockDefinition>();
        public SkillGrade DirectGrantDefaultGrade => SkillGradeUtility.Clamp(directGrantDefaultGrade);

        public bool HasAnyType(SkillType types)
        {
            return types != SkillType.Unknown && (skillTypes & types) != 0;
        }

        private void OnValidate()
        {
            naturalLearning?.Validate();
            defaultNaturalStartingGrade = SkillGradeUtility.Clamp(defaultNaturalStartingGrade);
            directGrantDefaultGrade = SkillGradeUtility.Clamp(directGrantDefaultGrade);

            if (xpThresholds != null)
            {
                for (int i = 0; i < xpThresholds.Length; i++)
                {
                    xpThresholds[i]?.Validate();
                }
            }

            if (gradePackages != null)
            {
                for (int i = 0; i < gradePackages.Length; i++)
                {
                    gradePackages[i]?.Validate();
                }
            }

            if (abilityUnlocks != null)
            {
                for (int i = 0; i < abilityUnlocks.Length; i++)
                {
                    abilityUnlocks[i]?.Validate();
                }
            }
        }

        public int GetXpThresholdFrom(SkillGrade fromGrade)
        {
            SkillGrade grade = SkillGradeUtility.Clamp(fromGrade);
            for (int i = 0; i < XpThresholds.Count; i++)
            {
                SkillXpThresholdDefinition threshold = XpThresholds[i];
                if (threshold != null && threshold.FromGrade == grade)
                {
                    return threshold.XpRequired;
                }
            }

            return 0;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            SkillDefinitionValidator.Validate(this, definitionsById, report);
        }
    }
}
