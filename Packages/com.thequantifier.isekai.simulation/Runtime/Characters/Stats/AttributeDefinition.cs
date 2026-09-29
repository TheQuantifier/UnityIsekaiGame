using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Stats
{
    [CreateAssetMenu(fileName = "AttributeDefinition", menuName = "Unity Isekai Game/Stats/Attribute Definition")]
    public sealed class AttributeDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string attributeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField, Min(0.0001f)] private float foundationValue = 1f;
        [SerializeField, Min(0f)] private float alphaActionGrowthAmount = 0.05f;
        [SerializeField] private AttributeDisplayRoundingPolicy displayRoundingPolicy = AttributeDisplayRoundingPolicy.Floor;
        [SerializeField, TextArea] private string contributionMetadata;

        public string Id => attributeId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Attribute;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public float FoundationValue => Mathf.Max(0.0001f, foundationValue);
        public float AlphaActionGrowthAmount => Mathf.Max(0f, alphaActionGrowthAmount);
        public AttributeDisplayRoundingPolicy DisplayRoundingPolicy => displayRoundingPolicy;
        public string ContributionMetadata => contributionMetadata ?? string.Empty;

        private void OnValidate()
        {
            foundationValue = Mathf.Max(0.0001f, foundationValue);
            alphaActionGrowthAmount = Mathf.Max(0f, alphaActionGrowthAmount);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Attribute '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("attribute."))
            {
                report.AddWarning($"Attribute '{Id}' should use the 'attribute.' namespace prefix.");
            }

            if (FoundationValue <= 0f)
            {
                report.AddError($"Attribute '{DisplayName}' foundation value must be above zero.");
            }
        }
    }
}
