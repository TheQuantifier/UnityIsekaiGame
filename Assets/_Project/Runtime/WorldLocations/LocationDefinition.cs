using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    [CreateAssetMenu(fileName = "LocationDefinition", menuName = "Unity Isekai Game/World/Location Definition")]
    public class LocationDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string locationDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private LocationCategory category = LocationCategory.FunctionalArea;
        [SerializeField] private bool persistent = true;
        [SerializeField] private bool allowsSceneBinding = true;
        [SerializeField] private bool validEntityLocation = true;
        [SerializeField] private bool validInteractionPointHost = true;
        [SerializeField] private bool futureContainmentAllowed = true;
        [SerializeField] private bool futureRoutingAllowed = true;
        [SerializeField] private bool supportsPropertyAssociation = true;
        [SerializeField] private bool supportsOrganizationAssociation = true;
        [SerializeField] private bool supportsGovernmentAssociation = true;
        [SerializeField] private bool supportsTerritoryAssociation = true;
        [SerializeField] private bool supportsPublicVisibility = true;
        [SerializeField] private bool supportsRestrictedVisibility = true;
        [SerializeField] private bool supportsSecretVisibility;
        [SerializeField] private bool supportsHiddenVisibility;
        [SerializeField] private string[] allowedSemanticTagIds;
        [SerializeField] private int version = 1;

        public string Id => locationDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Place;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public LocationCategory Category => category;
        public bool Persistent => persistent;
        public bool AllowsSceneBinding => allowsSceneBinding;
        public bool ValidEntityLocation => validEntityLocation;
        public bool ValidInteractionPointHost => validInteractionPointHost;
        public bool FutureContainmentAllowed => futureContainmentAllowed;
        public bool FutureRoutingAllowed => futureRoutingAllowed;
        public bool SupportsPropertyAssociation => supportsPropertyAssociation;
        public bool SupportsOrganizationAssociation => supportsOrganizationAssociation;
        public bool SupportsGovernmentAssociation => supportsGovernmentAssociation;
        public bool SupportsTerritoryAssociation => supportsTerritoryAssociation;
        public IReadOnlyList<string> AllowedSemanticTagIds => allowedSemanticTagIds ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            locationDefinitionId = locationDefinitionId?.Trim();
            displayName = displayName?.Trim();
            allowedSemanticTagIds = Clean(allowedSemanticTagIds);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            LocationCategory locationCategory,
            bool secretVisibility = false,
            bool hiddenVisibility = false,
            bool propertyAssociation = true,
            bool organizationAssociation = true,
            bool governmentAssociation = true,
            bool territoryAssociation = true,
            IEnumerable<string> semanticTags = null)
        {
            locationDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = locationCategory;
            persistent = true;
            allowsSceneBinding = true;
            validEntityLocation = locationCategory != LocationCategory.World && locationCategory != LocationCategory.Region;
            validInteractionPointHost = locationCategory == LocationCategory.Building || locationCategory == LocationCategory.Room || locationCategory == LocationCategory.FunctionalArea || locationCategory == LocationCategory.InteractionPoint;
            futureContainmentAllowed = true;
            futureRoutingAllowed = true;
            supportsPropertyAssociation = propertyAssociation;
            supportsOrganizationAssociation = organizationAssociation;
            supportsGovernmentAssociation = governmentAssociation;
            supportsTerritoryAssociation = territoryAssociation;
            supportsPublicVisibility = true;
            supportsRestrictedVisibility = true;
            supportsSecretVisibility = secretVisibility;
            supportsHiddenVisibility = hiddenVisibility;
            allowedSemanticTagIds = Clean(semanticTags);
            version = 1;
        }

        public bool SupportsVisibility(LocationVisibility visibility)
        {
            return visibility switch
            {
                LocationVisibility.Public => supportsPublicVisibility,
                LocationVisibility.Restricted => supportsRestrictedVisibility,
                LocationVisibility.Secret => supportsSecretVisibility,
                LocationVisibility.Hidden => supportsHiddenVisibility,
                _ => false
            };
        }

        public bool AllowsSemanticTag(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId))
            {
                return false;
            }

            return allowedSemanticTagIds == null || allowedSemanticTagIds.Length == 0 || allowedSemanticTagIds.Contains(tagId.Trim(), StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Location Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("location-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Location Definition '{Id}' should use the 'location-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(LocationCategory), category) || category == LocationCategory.Unknown)
            {
                report.AddError($"Location Definition '{DisplayName}' must declare a concrete category.");
            }

            if (!SupportsVisibility(LocationVisibility.Public))
            {
                report.AddError($"Location Definition '{DisplayName}' must support public visibility for baseline projection.");
            }

            foreach (string tag in Clean(allowedSemanticTagIds))
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddError($"Location Definition '{DisplayName}' has an empty semantic tag.");
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
