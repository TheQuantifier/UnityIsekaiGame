using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Abilities
{
    [CreateAssetMenu(fileName = "NewAbilityDefinition", menuName = "Unity Isekai Game/Abilities/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string abilityId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private CombatExecutionDefinition execution;
        [SerializeField, Min(0f)] private float range;
        [SerializeField] private AbilityTargetingMode targetingMode = AbilityTargetingMode.Direction;
        [SerializeField] private bool allowSelfTarget;
        [SerializeField] private bool requiresLineOfSight = true;
        [SerializeField] private LayerMask targetingMask = ~0;
        [SerializeField] private QueryTriggerInteraction targetingTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private AbilityDeliveryMode deliveryMode = AbilityDeliveryMode.Immediate;
        [SerializeField] private AbilityProjectileDelivery projectileDelivery;
        [SerializeField] private EffectDefinition[] effects;

        public string AbilityId => abilityId;
        public string Id => abilityId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Ability;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public CombatExecutionDefinition Execution => execution;
        public float Range => range;
        public AbilityTargetingMode TargetingMode => targetingMode;
        public bool AllowSelfTarget => allowSelfTarget;
        public bool RequiresLineOfSight => requiresLineOfSight;
        public LayerMask TargetingMask => targetingMask;
        public QueryTriggerInteraction TargetingTriggerInteraction => targetingTriggerInteraction;
        public AbilityDeliveryMode DeliveryMode => deliveryMode;
        public AbilityProjectileDelivery ProjectileDelivery => projectileDelivery;
        public IReadOnlyList<EffectDefinition> Effects => effects ?? System.Array.Empty<EffectDefinition>();

        private void OnValidate()
        {
            range = Mathf.Max(0f, range);
            projectileDelivery?.Validate();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            AbilityDefinitionValidator.ValidateAbility(this, definitionsById, report);
        }
    }
}
