using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Magic
{
    [CreateAssetMenu(fileName = "NewSpellDefinition", menuName = "Unity Isekai Game/Magic/Spell Definition")]
    public sealed class SpellDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string spellId;
        [SerializeField] private string displayName;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private AbilityDefinition ability;

        public string SpellId => spellId;
        public string Id => spellId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Ability;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public AbilityDefinition Ability => ability;
        public float ManaCost
        {
            get
            {
                if (ability?.Execution == null)
                {
                    return 0f;
                }

                float total = 0f;
                foreach (CombatExecutionCostDefinition cost in ability.Execution.Costs)
                {
                    if (cost.CostType == CombatExecutionCostType.Resource &&
                        cost.Resource != null && cost.Resource.Id == ResourceIds.Mana)
                    {
                        total += cost.Amount;
                    }
                }

                return total;
            }
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            AbilityDefinitionValidator.ValidateSpellAdapter(this, report);
        }
    }
}
