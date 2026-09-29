using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Reputation
{
    [CreateAssetMenu(fileName = "ReputationAudienceDefinition", menuName = "Unity Isekai Game/Social/Reputation Audience Definition")]
    public sealed class ReputationAudienceDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string audienceId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ReputationAudienceCategory category = ReputationAudienceCategory.GlobalPublic;
        [SerializeField] private ReputationAudienceScope scope = ReputationAudienceScope.Global;
        [SerializeField] private string contextDefinitionId;
        [SerializeField] private string parentAudienceId;
        [SerializeField] private bool supportsHierarchy;
        [SerializeField] private bool productionAvailable = true;
        [SerializeField] private bool restrictedVisibility;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => audienceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ReputationAudienceCategory Category => category;
        public ReputationAudienceScope Scope => scope;
        public string ContextDefinitionId => contextDefinitionId ?? string.Empty;
        public string ParentAudienceId => parentAudienceId ?? string.Empty;
        public bool SupportsHierarchy => supportsHierarchy;
        public bool ProductionAvailable => productionAvailable;
        public bool RestrictedVisibility => restrictedVisibility;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            audienceId = audienceId?.Trim();
            contextDefinitionId = contextDefinitionId?.Trim();
            parentAudienceId = parentAudienceId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            ReputationAudienceCategory audienceCategory,
            ReputationAudienceScope audienceScope,
            string contextId = "",
            string parentId = "",
            bool hierarchy = false,
            bool available = true,
            bool restricted = false,
            string text = "",
            IEnumerable<string> tagIds = null)
        {
            audienceId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = text ?? string.Empty;
            category = audienceCategory;
            scope = audienceScope;
            contextDefinitionId = contextId?.Trim() ?? string.Empty;
            parentAudienceId = parentId?.Trim() ?? string.Empty;
            supportsHierarchy = hierarchy;
            productionAvailable = available;
            restrictedVisibility = restricted;
            tags = Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Reputation Audience Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("reputation.audience.", StringComparison.Ordinal))
            {
                report.AddWarning($"Reputation Audience Definition '{Id}' should use the 'reputation.audience.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(ReputationAudienceCategory), category))
            {
                report.AddError($"Reputation Audience Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(ReputationAudienceScope), scope))
            {
                report.AddError($"Reputation Audience Definition '{DisplayName}' has invalid scope '{scope}'.");
            }

            bool requiresContext = scope == ReputationAudienceScope.Contextual || category != ReputationAudienceCategory.GlobalPublic;
            if (requiresContext && string.IsNullOrWhiteSpace(ContextDefinitionId))
            {
                report.AddError($"Reputation Audience Definition '{DisplayName}' requires an explicit context definition ID.");
            }

            if (!string.IsNullOrWhiteSpace(ParentAudienceId))
            {
                if (string.Equals(ParentAudienceId, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Reputation Audience Definition '{DisplayName}' cannot parent itself.");
                }
                else if (definitionsById == null || !definitionsById.TryGetValue(ParentAudienceId, out IGameDefinition parent) || parent is not ReputationAudienceDefinition)
                {
                    report.AddError($"Reputation Audience Definition '{DisplayName}' references missing parent audience '{ParentAudienceId}'.");
                }
                else if (!supportsHierarchy)
                {
                    report.AddError($"Reputation Audience Definition '{DisplayName}' has a parent but hierarchy support is disabled.");
                }
            }

            if (version < 1)
            {
                report.AddError($"Reputation Audience Definition '{DisplayName}' has invalid version '{version}'.");
            }

            foreach (string tag in tags ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddError($"Reputation Audience Definition '{DisplayName}' contains a blank tag.");
                }
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
