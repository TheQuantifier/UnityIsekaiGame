using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.OngoingEffects
{
    [CreateAssetMenu(fileName = "OngoingEffectDefinition", menuName = "Unity Isekai Game/Combat/Ongoing Effect")]
    public sealed class OngoingEffectDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string ongoingEffectId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private OngoingEffectOperationType operationType;
        [SerializeField] private ResourceDefinition targetResource;
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField, Min(0f)] private float amountPerTick = 1f;
        [SerializeField, Min(0.001f)] private float tickInterval = 1f;
        [SerializeField, Min(0f)] private float initialDelay;
        [SerializeField, Min(0f)] private float totalDuration = 5f;
        [SerializeField] private bool useFiniteTickCount;
        [SerializeField, Min(1)] private int finiteTickCount = 1;
        [SerializeField] private bool tickImmediately;
        [SerializeField] private OngoingEffectStackingPolicy stackingPolicy = OngoingEffectStackingPolicy.IndependentInstances;
        [SerializeField] private bool refreshDurationOnReapply = true;
        [SerializeField, Min(1)] private int maximumStacks = 1;
        [SerializeField] private OngoingEffectSourceOwnership sourceOwnership = OngoingEffectSourceOwnership.SourceAgnostic;
        [SerializeField] private OngoingEffectUnconsciousPolicy unconsciousPolicy = OngoingEffectUnconsciousPolicy.ContinueWhileUnconscious;
        [SerializeField] private OngoingEffectDeathPolicy deathPolicy = OngoingEffectDeathPolicy.CancelOnDeath;
        [SerializeField] private RequirementSetDefinition requirements;
        [SerializeField] private string[] requiredCapabilityIds;

        public string Id => ongoingEffectId;
        public string OngoingEffectId => ongoingEffectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.General;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public OngoingEffectOperationType OperationType => operationType;
        public ResourceDefinition TargetResource => targetResource;
        public string TargetResourceId => targetResource == null ? string.Empty : targetResource.Id;
        public DamageTypeDefinition DamageType => damageType;
        public float AmountPerTick => amountPerTick;
        public float TickInterval => tickInterval;
        public float InitialDelay => initialDelay > 0f ? initialDelay : tickInterval;
        public float TotalDuration => totalDuration;
        public bool UseFiniteTickCount => useFiniteTickCount;
        public int FiniteTickCount => Mathf.Max(1, finiteTickCount);
        public bool TickImmediately => tickImmediately;
        public OngoingEffectStackingPolicy StackingPolicy => stackingPolicy;
        public bool RefreshDurationOnReapply => refreshDurationOnReapply;
        public int MaximumStacks => Mathf.Max(1, maximumStacks);
        public OngoingEffectSourceOwnership SourceOwnership => sourceOwnership;
        public OngoingEffectUnconsciousPolicy UnconsciousPolicy => unconsciousPolicy;
        public OngoingEffectDeathPolicy DeathPolicy => deathPolicy;
        public RequirementSetDefinition Requirements => requirements;
        public IReadOnlyList<string> RequiredCapabilityIds => requiredCapabilityIds ?? Array.Empty<string>();
        public bool HasDurationLimit => totalDuration > 0f;

        private void OnValidate()
        {
            ongoingEffectId = ongoingEffectId?.Trim();
            amountPerTick = Mathf.Max(0f, amountPerTick);
            tickInterval = Mathf.Max(0.001f, tickInterval);
            initialDelay = Mathf.Max(0f, initialDelay);
            totalDuration = Mathf.Max(0f, totalDuration);
            finiteTickCount = Mathf.Max(1, finiteTickCount);
            maximumStacks = Mathf.Max(1, maximumStacks);
        }

        public float ResolveAmount(float overrideAmount, int stackCount)
        {
            float baseAmount = overrideAmount > 0f ? overrideAmount : amountPerTick;
            return Mathf.Max(0f, baseAmount) * Mathf.Max(1, stackCount);
        }

        public float ResolveInterval(float overrideInterval)
        {
            return overrideInterval > 0f ? overrideInterval : tickInterval;
        }

        public float ResolveDuration(float overrideDuration)
        {
            return overrideDuration > 0f ? overrideDuration : totalDuration;
        }

        public int ResolveTickCount(int overrideTickCount)
        {
            return overrideTickCount > 0 ? overrideTickCount : useFiniteTickCount ? FiniteTickCount : 0;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("ongoing-effect.", StringComparison.Ordinal))
            {
                report.AddWarning($"OngoingEffectDefinition '{DisplayName}' should use the 'ongoing-effect.' namespace prefix.");
            }

            if (primaryCategory == null)
            {
                report.AddWarning($"OngoingEffectDefinition '{DisplayName}' has no category.");
            }

            if (!Enum.IsDefined(typeof(OngoingEffectOperationType), operationType))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has an invalid operation type '{operationType}'.");
            }

            if (targetResource == null)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has no target Resource.");
            }
            else
            {
                ValidateDefinitionReference<ResourceDefinition>(targetResource, "Resource", definitionsById, report);
                if (targetResource.Id != ResourceIds.Health && targetResource.Id != ResourceIds.Mana && targetResource.Id != ResourceIds.Stamina)
                {
                    report.AddError($"OngoingEffectDefinition '{DisplayName}' targets unsupported Resource '{targetResource.Id}'. Feature 6.4 supports Health, Mana, and Stamina.");
                }
            }

            if (operationType == OngoingEffectOperationType.Damage && damageType == null)
            {
                report.AddError($"Damage ongoing effect '{DisplayName}' has no Damage Type.");
            }

            if (damageType != null)
            {
                ValidateDefinitionReference<DamageTypeDefinition>(damageType, "Damage Type", definitionsById, report);
            }

            if (!IsFinite(amountPerTick) || amountPerTick <= 0f)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' must have a positive finite amount per tick.");
            }

            if (!IsFinite(tickInterval) || tickInterval <= 0f)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' must have a positive finite tick interval.");
            }

            if (!IsFinite(totalDuration) || (!useFiniteTickCount && totalDuration <= 0f))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' must have a positive duration unless finite tick count is enabled.");
            }

            if (useFiniteTickCount && finiteTickCount < 1)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' uses finite tick count but has no positive tick count.");
            }

            if (stackingPolicy == OngoingEffectStackingPolicy.AddStacks && maximumStacks < 2)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' uses AddStacks but maximum stacks is below two.");
            }

            if (!Enum.IsDefined(typeof(OngoingEffectStackingPolicy), stackingPolicy))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has an invalid stacking policy '{stackingPolicy}'.");
            }

            if (!Enum.IsDefined(typeof(OngoingEffectSourceOwnership), sourceOwnership))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has an invalid source ownership policy '{sourceOwnership}'.");
            }

            if (!Enum.IsDefined(typeof(OngoingEffectUnconsciousPolicy), unconsciousPolicy))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has an invalid unconscious lifecycle policy '{unconsciousPolicy}'.");
            }

            if (!Enum.IsDefined(typeof(OngoingEffectDeathPolicy), deathPolicy))
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' has an invalid death lifecycle policy '{deathPolicy}'.");
            }

            if (deathPolicy == OngoingEffectDeathPolicy.ContinueAfterDeath && operationType != OngoingEffectOperationType.Damage)
            {
                report.AddWarning($"OngoingEffectDefinition '{DisplayName}' continues after death; verify this is intentional.");
            }

            ValidateCapabilities(report);
            ValidateRequirementReference(requirements, definitionsById, report);
        }

        private void ValidateCapabilities(DefinitionValidationReport report)
        {
            if (requiredCapabilityIds == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requiredCapabilityIds.Length; i++)
            {
                string key = requiredCapabilityIds[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    report.AddError($"OngoingEffectDefinition '{DisplayName}' has an empty required Capability key at index {i}.");
                    continue;
                }

                if (!key.StartsWith("capability.", StringComparison.Ordinal))
                {
                    report.AddWarning($"OngoingEffectDefinition '{DisplayName}' Capability key '{key}' should use 'capability.' prefix.");
                }

                if (!seen.Add(key))
                {
                    report.AddWarning($"OngoingEffectDefinition '{DisplayName}' has duplicate Capability key '{key}'.");
                }
            }
        }

        private void ValidateRequirementReference(RequirementSetDefinition requirement, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (requirement == null)
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(requirement.Id, out IGameDefinition found)
                || found is not RequirementSetDefinition)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' references RequirementSet '{requirement.Id}', which is not in the configured catalog.");
            }
        }

        private void ValidateDefinitionReference<TDefinition>(IGameDefinition definition, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            if (definition == null)
            {
                return;
            }

            if (definitionsById == null
                || !definitionsById.TryGetValue(definition.Id, out IGameDefinition found)
                || found is not TDefinition)
            {
                report.AddError($"OngoingEffectDefinition '{DisplayName}' references {label} '{definition.Id}', which is not in the configured catalog.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
