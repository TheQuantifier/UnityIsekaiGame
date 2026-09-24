using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.StatusEffects
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Unity Isekai Game/Status Effects/Status Effect")]
    public sealed class StatusEffectDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string statusId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private StatusEffectDisposition disposition;
        [SerializeField] private StatusDurationModel durationModel = StatusDurationModel.Timed;
        [SerializeField, Min(0f)] private float defaultDuration = 8f;
        [SerializeField] private StatusStackingPolicy stackingPolicy = StatusStackingPolicy.RefreshDuration;
        [SerializeField] private StatusRefreshPolicy refreshPolicy = StatusRefreshPolicy.ResetToFullDuration;
        [SerializeField] private StatusPersistencePolicy persistencePolicy = StatusPersistencePolicy.SaveRemainingDuration;
        [SerializeField, Min(1)] private int maximumStacks = 1;
        [SerializeField] private bool canBeRemoved = true;
        [SerializeField] private bool visibleInHud = true;
        [SerializeField] private CalculatedStatModifierDefinition[] calculatedStatModifiers;
        [SerializeField] private ResistanceModifierDefinition[] resistanceModifiers;
        [SerializeField] private EffectDefinition[] instantEffects;
        [SerializeField] private OngoingEffectDefinition[] ongoingEffects;

        public string StatusId => statusId;
        public string Id => statusId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.General;
        public IReadOnlyList<TagDefinition> Tags => tags ?? System.Array.Empty<TagDefinition>();
        public StatusEffectDisposition Disposition => disposition;
        public StatusDurationModel DurationModel => durationModel;
        public float DefaultDuration => defaultDuration;
        public StatusStackingPolicy StackingPolicy => stackingPolicy;
        public StatusRefreshPolicy RefreshPolicy => refreshPolicy;
        public StatusPersistencePolicy PersistencePolicy => persistencePolicy;
        public int MaximumStacks => Mathf.Max(1, maximumStacks);
        public bool CanBeRemoved => canBeRemoved;
        public bool VisibleInHud => visibleInHud;
        public IReadOnlyList<CalculatedStatModifierDefinition> CalculatedStatModifiers => calculatedStatModifiers ?? System.Array.Empty<CalculatedStatModifierDefinition>();
        public IReadOnlyList<ResistanceModifierDefinition> ResistanceModifiers => resistanceModifiers ?? System.Array.Empty<ResistanceModifierDefinition>();
        public IReadOnlyList<EffectDefinition> InstantEffects => instantEffects ?? System.Array.Empty<EffectDefinition>();
        public IReadOnlyList<OngoingEffectDefinition> OngoingEffects => ongoingEffects ?? System.Array.Empty<OngoingEffectDefinition>();

        private void OnValidate()
        {
            defaultDuration = Mathf.Max(0f, defaultDuration);
            maximumStacks = Mathf.Max(1, maximumStacks);
        }

        public float ResolveDuration(float overrideDuration)
        {
            return overrideDuration > 0f ? overrideDuration : defaultDuration;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (primaryCategory == null)
            {
                report.AddError($"Status effect '{DisplayName}' has no category.");
            }

            if (durationModel == StatusDurationModel.Timed && defaultDuration <= 0f)
            {
                report.AddError($"Timed status effect '{DisplayName}' must have a positive default duration.");
            }

            if (durationModel == StatusDurationModel.Instant && persistencePolicy != StatusPersistencePolicy.DoNotSave)
            {
                report.AddWarning($"Instant status effect '{DisplayName}' should normally use DoNotSave persistence.");
            }

            if (durationModel == StatusDurationModel.Instant && CalculatedStatModifiers.Count > 0)
            {
                report.AddWarning($"Instant status effect '{DisplayName}' has stat modifiers that will not remain active.");
            }

            if (durationModel == StatusDurationModel.Instant && (resistanceModifiers?.Length ?? 0) > 0)
            {
                report.AddWarning($"Instant status effect '{DisplayName}' has resistance modifiers that will not remain active.");
            }

            if (maximumStacks < 1)
            {
                report.AddError($"Status effect '{DisplayName}' must allow at least one stack.");
            }

            if (stackingPolicy == StatusStackingPolicy.AddStack && maximumStacks < 2)
            {
                report.AddError($"Status effect '{DisplayName}' uses AddStack but maximum stacks is below two.");
            }

            if (stackingPolicy != StatusStackingPolicy.AddStack && maximumStacks > 1)
            {
                report.AddWarning($"Status effect '{DisplayName}' has maximum stacks above one but does not use AddStack.");
            }

            ValidateModifierDefinitions(definitionsById, report);
            ValidateResistanceModifierDefinitions(definitionsById, report);
            ValidateEffectConfiguration(definitionsById, report);
        }

        private void ValidateModifierDefinitions(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (calculatedStatModifiers == null)
            {
                return;
            }

            HashSet<string> seenModifiers = new HashSet<string>();
            for (int i = 0; i < calculatedStatModifiers.Length; i++)
            {
                CalculatedStatModifierDefinition modifier = calculatedStatModifiers[i];
                if (modifier == null)
                {
                    report.AddError($"Status effect '{DisplayName}' has a null stat modifier at index {i}.");
                    continue;
                }

                if (!modifier.IsValid)
                {
                    report.AddError($"Status effect '{DisplayName}' has an invalid modifier value at index {i}.");
                }

                if (modifier.Stat != null && (definitionsById == null || !definitionsById.TryGetValue(modifier.Stat.Id, out IGameDefinition registeredStat) || !ReferenceEquals(registeredStat, modifier.Stat)))
                {
                    report.AddError($"Status effect '{DisplayName}' references calculated stat '{modifier.Stat.Id}' outside the configured catalog.");
                }

                string key = $"{modifier.Stat?.Id}:{modifier.Operation}:{modifier.Value}:{modifier.Priority}";
                if (!seenModifiers.Add(key))
                {
                    report.AddWarning($"Status effect '{DisplayName}' has duplicate-looking stat modifier '{key}'.");
                }
            }
        }

        private void ValidateEffectConfiguration(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (durationModel != StatusDurationModel.Instant && instantEffects != null && instantEffects.Length > 0)
            {
                report.AddError($"Non-instant status effect '{DisplayName}' cannot author instant effects.");
            }

            if (durationModel == StatusDurationModel.Instant && ongoingEffects != null && ongoingEffects.Length > 0)
            {
                report.AddError($"Instant status effect '{DisplayName}' cannot own ongoing effects.");
            }

            if (instantEffects != null)
            {
                for (int i = 0; i < instantEffects.Length; i++)
                {
                    if (instantEffects[i] == null)
                    {
                        report.AddError($"Status effect '{DisplayName}' has a null instant effect at index {i}.");
                    }
                }
            }

            if (ongoingEffects == null)
            {
                return;
            }

            for (int i = 0; i < ongoingEffects.Length; i++)
            {
                OngoingEffectDefinition ongoing = ongoingEffects[i];
                if (ongoing == null)
                {
                    report.AddError($"Status effect '{DisplayName}' has a null ongoing effect at index {i}.");
                }
                else if (definitionsById == null || !definitionsById.TryGetValue(ongoing.Id, out IGameDefinition found) || found is not OngoingEffectDefinition)
                {
                    report.AddError($"Status effect '{DisplayName}' references ongoing effect '{ongoing.Id}' outside the configured catalog.");
                }
            }
        }

        private void ValidateResistanceModifierDefinitions(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (resistanceModifiers == null)
            {
                return;
            }

            HashSet<string> seenModifiers = new HashSet<string>();
            for (int i = 0; i < resistanceModifiers.Length; i++)
            {
                ResistanceModifierDefinition modifier = resistanceModifiers[i];
                if (modifier == null)
                {
                    report.AddError($"Status effect '{DisplayName}' has a null resistance modifier at index {i}.");
                    continue;
                }

                if (modifier.DamageType == null)
                {
                    report.AddError($"Status effect '{DisplayName}' has a resistance modifier with no damage type at index {i}.");
                    continue;
                }

                if (!modifier.IsValid)
                {
                    report.AddError($"Status effect '{DisplayName}' has an invalid resistance modifier value at index {i}.");
                }

                if (!seenModifiers.Add($"{modifier.DamageType.Id}:{modifier.Priority}"))
                {
                    report.AddWarning($"Status effect '{DisplayName}' has duplicate-looking resistance modifier for '{modifier.DamageType.Id}'.");
                }

                if (definitionsById == null
                    || !definitionsById.TryGetValue(modifier.DamageType.Id, out IGameDefinition found)
                    || found is not DamageTypeDefinition)
                {
                    report.AddError($"Status effect '{DisplayName}' references damage type '{modifier.DamageType.Id}', which is not in the configured catalog.");
                }
            }
        }
    }
}
