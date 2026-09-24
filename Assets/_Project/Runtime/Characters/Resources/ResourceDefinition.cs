using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.ResourceSystem
{
    [CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Unity Isekai Game/Resources/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string resourceId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField] private ResourceCategoryKind resourceCategory = ResourceCategoryKind.Vital;
        [SerializeField] private CalculatedStatDefinition linkedMaximumStat;
        [SerializeField, Min(0f)] private float minimumValue;
        [SerializeField, Min(0f)] private float defaultMaximum = 100f;
        [SerializeField] private ResourceInitializationPolicy initializationPolicy = ResourceInitializationPolicy.Full;
        [SerializeField, Min(0f)] private float initialFixedValue;
        [SerializeField, Range(0f, 1f)] private float initialPercentageOfMaximum = 1f;
        [SerializeField] private ResourceMaximumReconciliationPolicy maximumReconciliationPolicy = ResourceMaximumReconciliationPolicy.ClampOnly;
        [SerializeField] private bool regenerationEnabled;
        [SerializeField, Min(0f)] private float regenerationPerSecond;
        [SerializeField, Min(0f)] private float regenerationInterval = 0.25f;
        [SerializeField, Min(0f)] private float regenerationDelayAfterSpend;
        [SerializeField, Min(0f)] private float regenerationDelayAfterDamage;
        [SerializeField] private bool degenerationEnabled;
        [SerializeField, Min(0f)] private float degenerationPerSecond;
        [SerializeField, Min(0f)] private float degenerationInterval = 0.25f;
        [SerializeField] private bool spendAllowed = true;
        [SerializeField] private bool gainAllowed = true;
        [SerializeField] private bool damageAllowed = true;
        [SerializeField] private bool healingAllowed = true;
        [SerializeField] private bool overfillAllowed;
        [SerializeField] private bool underflowAllowed;
        [SerializeField] private ResourcePersistencePolicy persistencePolicy = ResourcePersistencePolicy.Persist;
        [SerializeField] private ResourceAuthorityKind authority = ResourceAuthorityKind.ServerAuthoritative;
        [SerializeField] private string uiColor = "#FFFFFF";

        public string Id => resourceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Resource;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public bool AlphaEnabled => alphaEnabled;
        public ResourceCategoryKind ResourceCategory => resourceCategory;
        public CalculatedStatDefinition LinkedMaximumStat => linkedMaximumStat;
        public string LinkedMaximumStatId => linkedMaximumStat == null ? string.Empty : linkedMaximumStat.Id;
        public float MinimumValue => Mathf.Max(0f, minimumValue);
        public float DefaultMaximum => Mathf.Max(MinimumValue, defaultMaximum);
        public ResourceInitializationPolicy InitializationPolicy => initializationPolicy;
        public float InitialFixedValue => initialFixedValue;
        public float InitialPercentageOfMaximum => Mathf.Clamp01(initialPercentageOfMaximum);
        public ResourceMaximumReconciliationPolicy MaximumReconciliationPolicy => maximumReconciliationPolicy;
        public bool RegenerationEnabled => regenerationEnabled;
        public float RegenerationPerSecond => Mathf.Max(0f, regenerationPerSecond);
        public float RegenerationInterval => Mathf.Max(0.01f, regenerationInterval);
        public float RegenerationDelayAfterSpend => Mathf.Max(0f, regenerationDelayAfterSpend);
        public float RegenerationDelayAfterDamage => Mathf.Max(0f, regenerationDelayAfterDamage);
        public bool DegenerationEnabled => degenerationEnabled;
        public float DegenerationPerSecond => Mathf.Max(0f, degenerationPerSecond);
        public float DegenerationInterval => Mathf.Max(0.01f, degenerationInterval);
        public bool SpendAllowed => spendAllowed;
        public bool GainAllowed => gainAllowed;
        public bool DamageAllowed => damageAllowed;
        public bool HealingAllowed => healingAllowed;
        public bool OverfillAllowed => overfillAllowed;
        public bool UnderflowAllowed => underflowAllowed;
        public ResourcePersistencePolicy PersistencePolicy => persistencePolicy;
        public ResourceAuthorityKind Authority => authority;
        public string UiColor => uiColor ?? string.Empty;

        private void OnValidate()
        {
            minimumValue = Mathf.Max(0f, minimumValue);
            defaultMaximum = Mathf.Max(minimumValue, defaultMaximum);
            initialFixedValue = Mathf.Max(0f, initialFixedValue);
            initialPercentageOfMaximum = Mathf.Clamp01(initialPercentageOfMaximum);
            regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
            regenerationInterval = Mathf.Max(0.01f, regenerationInterval);
            regenerationDelayAfterSpend = Mathf.Max(0f, regenerationDelayAfterSpend);
            regenerationDelayAfterDamage = Mathf.Max(0f, regenerationDelayAfterDamage);
            degenerationPerSecond = Mathf.Max(0f, degenerationPerSecond);
            degenerationInterval = Mathf.Max(0.01f, degenerationInterval);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Resource '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("resource."))
            {
                report.AddWarning($"Resource '{Id}' should use the 'resource.' namespace prefix.");
            }

            if (!IsFinite(MinimumValue))
            {
                report.AddError($"Resource '{DisplayName}' has a non-finite minimum value.");
            }

            if (linkedMaximumStat == null)
            {
                report.AddError($"Resource '{DisplayName}' is missing a linked maximum Calculated Stat.");
            }
            else
            {
                if (definitionsById == null || !definitionsById.TryGetValue(linkedMaximumStat.Id, out IGameDefinition found) || found is not CalculatedStatDefinition)
                {
                    report.AddError($"Resource '{DisplayName}' references maximum stat '{linkedMaximumStat.Id}', which is not in the configured catalog.");
                }

                if (!linkedMaximumStat.IsResourceMaximum)
                {
                    report.AddError($"Resource '{DisplayName}' links calculated stat '{linkedMaximumStat.Id}', but that stat is not classified as ResourceMaximum.");
                }

                if (!string.Equals(linkedMaximumStat.LinkedResourceId, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Resource '{DisplayName}' ID '{Id}' does not match linked maximum stat resource link '{linkedMaximumStat.LinkedResourceId}'.");
                }
            }

            if (!Enum.IsDefined(typeof(ResourceInitializationPolicy), initializationPolicy))
            {
                report.AddError($"Resource '{DisplayName}' has an invalid initialization policy.");
            }

            if (!Enum.IsDefined(typeof(ResourceMaximumReconciliationPolicy), maximumReconciliationPolicy))
            {
                report.AddError($"Resource '{DisplayName}' has an invalid maximum reconciliation policy.");
            }

            if (initializationPolicy == ResourceInitializationPolicy.PercentageOfMaximum && !IsFinite(initialPercentageOfMaximum))
            {
                report.AddError($"Resource '{DisplayName}' has an invalid initialization percentage.");
            }

            if (!IsFinite(initialFixedValue))
            {
                report.AddError($"Resource '{DisplayName}' has an invalid fixed initialization value.");
            }

            if (regenerationEnabled && (RegenerationPerSecond <= 0f || RegenerationInterval <= 0f))
            {
                report.AddError($"Resource '{DisplayName}' has regeneration enabled without a positive rate and interval.");
            }

            if (degenerationEnabled && (DegenerationPerSecond <= 0f || DegenerationInterval <= 0f))
            {
                report.AddError($"Resource '{DisplayName}' has degeneration enabled without a positive rate and interval.");
            }

            if (overfillAllowed && maximumReconciliationPolicy == ResourceMaximumReconciliationPolicy.RefillToMaximum)
            {
                report.AddWarning($"Resource '{DisplayName}' allows overfill while using refill reconciliation; verify this is intentional.");
            }

            ValidateDuplicateMaximumClaim(definitionsById, report);
        }

        private void ValidateDuplicateMaximumClaim(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || linkedMaximumStat == null)
            {
                return;
            }

            foreach (ResourceDefinition other in definitionsById.Values.OfType<ResourceDefinition>())
            {
                if (other == null || other == this || other.LinkedMaximumStat == null)
                {
                    continue;
                }

                if (string.Equals(other.LinkedMaximumStat.Id, linkedMaximumStat.Id, StringComparison.Ordinal))
                {
                    report.AddError($"Resource '{DisplayName}' and resource '{other.Id}' both claim maximum stat '{linkedMaximumStat.Id}'.");
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
