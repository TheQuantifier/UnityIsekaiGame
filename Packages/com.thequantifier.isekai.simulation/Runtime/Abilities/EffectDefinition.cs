using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Abilities
{
    public abstract class EffectDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string effectId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;

        public string EffectId => effectId;
        public string Id => effectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Ability;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();

        public abstract EffectExecutionResult CanExecute(in EffectExecutionContext context);
        public abstract EffectExecutionResult Execute(in EffectExecutionContext context);

        public virtual void ValidateDefinition(DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                report?.AddError($"{GetType().Name} '{name}' is missing an effect ID.");
            }

            if (primaryCategory == null)
            {
                report?.AddError($"{GetType().Name} '{DisplayName}' is missing an ability/effect category.");
            }
        }

        public virtual void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            AbilityDefinitionValidator.ValidateEffect(this, definitionsById, report);
        }
    }
}
