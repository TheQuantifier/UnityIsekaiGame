using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Stats
{
    [CreateAssetMenu(fileName = "CalculatedStatFormulaDefinition", menuName = "Unity Isekai Game/Stats/Calculated Stat Formula")]
    public sealed class CalculatedStatFormulaDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string formulaId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private float baseValue;
        [SerializeField] private AttributeFormulaTerm[] attributeTerms;
        [SerializeField] private CalculatedStatRoundingPolicy roundingPolicy = CalculatedStatRoundingPolicy.NearestWhole;
        [SerializeField] private bool clampMinimumToZero = true;

        public string Id => formulaId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.CalculatedStat;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public float BaseValue => IsFinite(baseValue) ? baseValue : 0f;
        public IReadOnlyList<AttributeFormulaTerm> AttributeTerms => attributeTerms ?? System.Array.Empty<AttributeFormulaTerm>();
        public CalculatedStatRoundingPolicy RoundingPolicy => roundingPolicy;
        public bool ClampMinimumToZero => clampMinimumToZero;

        private void OnValidate()
        {
            if (!IsFinite(baseValue))
            {
                baseValue = 0f;
            }
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Calculated stat formula '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("calculated-stat-formula."))
            {
                report.AddWarning($"Calculated stat formula '{Id}' should use the 'calculated-stat-formula.' namespace prefix.");
            }

            if (!IsFinite(baseValue))
            {
                report.AddError($"Calculated stat formula '{DisplayName}' has an invalid base value.");
            }

            HashSet<string> seenAttributeIds = new HashSet<string>();
            foreach (AttributeFormulaTerm term in AttributeTerms)
            {
                if (term == null || !term.IsValid)
                {
                    report.AddError($"Calculated stat formula '{DisplayName}' has an invalid attribute term.");
                    continue;
                }

                string attributeId = term.Attribute.Id;
                if (!seenAttributeIds.Add(attributeId))
                {
                    report.AddError($"Calculated stat formula '{DisplayName}' has duplicate attribute term '{attributeId}'.");
                }

                if (definitionsById == null || !definitionsById.TryGetValue(attributeId, out IGameDefinition found) || found is not AttributeDefinition)
                {
                    report.AddError($"Calculated stat formula '{DisplayName}' references attribute '{attributeId}', which is not in the configured catalog.");
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
