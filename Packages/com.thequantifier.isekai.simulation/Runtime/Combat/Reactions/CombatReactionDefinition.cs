using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Reactions
{
    [CreateAssetMenu(fileName = "CombatReactionDefinition", menuName = "Unity Isekai Game/Combat/Reaction")]
    public sealed class CombatReactionDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string reactionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private CombatReactionTriggerType[] triggerTypes = Array.Empty<CombatReactionTriggerType>();
        [SerializeField] private CombatReactionOwnershipSide ownershipSide = CombatReactionOwnershipSide.Any;
        [SerializeField] private CombatReactionTargetPolicy targetPolicy = CombatReactionTargetPolicy.OriginalTarget;
        [SerializeField, Range(0f, 1f)] private float procChance = 1f;
        [SerializeField] private int priority;
        [SerializeField] private CombatReactionOperationType operationType = CombatReactionOperationType.NoOpDiagnostic;
        [SerializeField, Min(0f)] private float amount;
        [SerializeField, Min(0f)] private float multiplier;
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField] private OngoingEffectDefinition ongoingEffect;
        [SerializeField] private ResourceDefinition resource;
        [SerializeField] private RequirementSetDefinition requirements;
        [SerializeField] private string[] requiredCapabilityIds;
        [SerializeField] private string[] excludedCapabilityIds;
        [SerializeField] private string[] requiredContextTags;
        [SerializeField] private string[] excludedContextTags;
        [SerializeField, Min(1)] private int maximumExecutionsPerChain = 1;
        [SerializeField] private CombatReactionRecursionPolicy recursionPolicy = CombatReactionRecursionPolicy.OncePerSourcePerChain;

        public string Id => reactionId;
        public string ReactionId => reactionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.General;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<CombatReactionTriggerType> TriggerTypes => triggerTypes ?? Array.Empty<CombatReactionTriggerType>();
        public CombatReactionOwnershipSide OwnershipSide => ownershipSide;
        public CombatReactionTargetPolicy TargetPolicy => targetPolicy;
        public float ProcChance => procChance;
        public int Priority => priority;
        public CombatReactionOperationType OperationType => operationType;
        public float Amount => amount;
        public float Multiplier => multiplier;
        public DamageTypeDefinition DamageType => damageType;
        public OngoingEffectDefinition OngoingEffect => ongoingEffect;
        public ResourceDefinition Resource => resource;
        public string ResourceId => resource == null ? string.Empty : resource.Id;
        public RequirementSetDefinition Requirements => requirements;
        public IReadOnlyList<string> RequiredCapabilityIds => requiredCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ExcludedCapabilityIds => excludedCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredContextTags => requiredContextTags ?? Array.Empty<string>();
        public IReadOnlyList<string> ExcludedContextTags => excludedContextTags ?? Array.Empty<string>();
        public int MaximumExecutionsPerChain => Mathf.Max(1, maximumExecutionsPerChain);
        public CombatReactionRecursionPolicy RecursionPolicy => recursionPolicy;

        public bool SupportsTrigger(CombatReactionTriggerType triggerType)
        {
            IReadOnlyList<CombatReactionTriggerType> triggers = TriggerTypes;
            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] == triggerType)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            reactionId = reactionId?.Trim();
            amount = Mathf.Max(0f, amount);
            multiplier = Mathf.Max(0f, multiplier);
            procChance = Mathf.Clamp01(procChance);
            maximumExecutionsPerChain = Mathf.Max(1, maximumExecutionsPerChain);
            TrimArray(requiredCapabilityIds);
            TrimArray(excludedCapabilityIds);
            TrimArray(requiredContextTags);
            TrimArray(excludedContextTags);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("combat-reaction.", StringComparison.Ordinal))
            {
                report.AddWarning($"CombatReactionDefinition '{DisplayName}' should use the 'combat-reaction.' namespace prefix.");
            }

            if (triggerTypes == null || triggerTypes.Length == 0)
            {
                report.AddError($"CombatReactionDefinition '{DisplayName}' has no trigger types.");
            }

            if (!IsFinite(procChance) || procChance < 0f || procChance > 1f)
            {
                report.AddError($"CombatReactionDefinition '{DisplayName}' must use a proc chance in [0,1].");
            }

            if (!IsFinite(amount) || !IsFinite(multiplier))
            {
                report.AddError($"CombatReactionDefinition '{DisplayName}' uses a non-finite amount or multiplier.");
            }

            if (operationType == CombatReactionOperationType.ApplyDamage && damageType == null)
            {
                report.AddError($"Damage CombatReactionDefinition '{DisplayName}' has no Damage Type.");
            }

            if (operationType == CombatReactionOperationType.ApplyOngoingEffect && ongoingEffect == null)
            {
                report.AddError($"Ongoing-effect CombatReactionDefinition '{DisplayName}' has no Ongoing Effect definition.");
            }

            if (operationType == CombatReactionOperationType.ModifyResource && resource == null)
            {
                report.AddError($"Resource CombatReactionDefinition '{DisplayName}' has no Resource definition.");
            }

            if (operationType == CombatReactionOperationType.ApplyStatusEffect
                || operationType == CombatReactionOperationType.RemoveStatusEffect
                || operationType == CombatReactionOperationType.ApplyCondition
                || operationType == CombatReactionOperationType.RemoveCondition
                || operationType == CombatReactionOperationType.TriggerImmediateAbility)
            {
                report.AddError($"CombatReactionDefinition '{DisplayName}' uses deferred operation '{operationType}'. Runtime will reject it until a safe production API is available.");
            }

            ValidateDefinitionReference<DamageTypeDefinition>(damageType, "Damage Type", definitionsById, report);
            ValidateDefinitionReference<OngoingEffectDefinition>(ongoingEffect, "Ongoing Effect", definitionsById, report);
            ValidateDefinitionReference<ResourceDefinition>(resource, "Resource", definitionsById, report);
            ValidateDefinitionReference<RequirementSetDefinition>(requirements, "Requirement Set", definitionsById, report);
            ValidateStringSet(requiredCapabilityIds, "required Capability", "capability.", report);
            ValidateStringSet(excludedCapabilityIds, "excluded Capability", "capability.", report);
        }

        private void ValidateDefinitionReference<T>(T definition, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where T : UnityEngine.Object, IGameDefinition
        {
            if (definition == null)
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(definition.Id, out IGameDefinition found)
                || found is not T)
            {
                report.AddError($"CombatReactionDefinition '{DisplayName}' references {label} '{definition.Id}', which is not in the configured catalog.");
            }
        }

        private void ValidateStringSet(string[] values, string label, string expectedPrefix, DefinitionValidationReport report)
        {
            if (values == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    report.AddError($"CombatReactionDefinition '{DisplayName}' has an empty {label} at index {i}.");
                    continue;
                }

                if (!value.StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    report.AddWarning($"CombatReactionDefinition '{DisplayName}' {label} '{value}' should use '{expectedPrefix}' prefix.");
                }

                if (!seen.Add(value))
                {
                    report.AddWarning($"CombatReactionDefinition '{DisplayName}' has duplicate {label} '{value}'.");
                }
            }
        }

        private static void TrimArray(string[] values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i]?.Trim();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
