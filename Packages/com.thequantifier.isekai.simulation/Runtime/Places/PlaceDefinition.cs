using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Places
{
    [CreateAssetMenu(fileName = "Place", menuName = "Unity Isekai Game/Places/Place")]
    public sealed class PlaceDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string placeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private Sprite icon;
        [SerializeField] private PlaceDefinition parentPlace;
        [SerializeField] private PlaceKind placeKind = PlaceKind.Unspecified;
        [SerializeField] private string sceneKey;
        [SerializeField] private string mapLabel;
        [SerializeField] private bool showOnMapByDefault;
        [SerializeField] private Vector2 normalizedMapPosition = new Vector2(0.5f, 0.5f);
        [SerializeField] private FactionDefinition defaultGoverningFaction;
        [SerializeField] private string controllingFactionIdPlaceholder;
        [SerializeField] private PlaceDangerLevel dangerLevel = PlaceDangerLevel.Unknown;
        [SerializeField] private PlaceDiscoveryMode discoveryMode = PlaceDiscoveryMode.KnownByDefault;

        public string PlaceId => placeId;
        public string Id => placeId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Place;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public Sprite Icon => icon;
        public PlaceDefinition ParentPlace => parentPlace;
        public PlaceKind PlaceKind => placeKind;
        public string SceneKey => sceneKey;
        public string MapLabel => string.IsNullOrWhiteSpace(mapLabel) ? DisplayName : mapLabel;
        public bool ShowOnMapByDefault => showOnMapByDefault;
        public Vector2 NormalizedMapPosition => normalizedMapPosition;
        public FactionDefinition DefaultGoverningFaction => defaultGoverningFaction;
        public string ControllingFactionIdPlaceholder => controllingFactionIdPlaceholder;
        public PlaceDangerLevel DangerLevel => dangerLevel;
        public PlaceDiscoveryMode DiscoveryMode => discoveryMode;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || report == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(Id) && !Id.StartsWith("place."))
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' should use the 'place.' namespace prefix.");
            }

            if (parentPlace != null)
            {
                if (ReferenceEquals(this, parentPlace) || parentPlace.Id == Id)
                {
                    report.AddError($"PlaceDefinition '{DisplayName}' cannot parent itself.");
                }
                else if (!definitionsById.TryGetValue(parentPlace.Id, out IGameDefinition parent) || !(parent is PlaceDefinition))
                {
                    report.AddError($"PlaceDefinition '{DisplayName}' references parent place '{parentPlace.Id}', which is not in the configured catalog.");
                }

                if (PlaceHierarchyUtility.HasParentCycle(this))
                {
                    report.AddError($"PlaceDefinition '{DisplayName}' has a circular parent hierarchy.");
                }

                ValidateSuspiciousHierarchy(report);
            }

            if (placeKind == PlaceKind.Unspecified)
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' has unspecified place kind metadata.");
            }

            if (primaryCategory != null && placeKind != PlaceKind.Unspecified)
            {
                ValidateKindCategoryConsistency(report);
            }

            if (!string.IsNullOrWhiteSpace(sceneKey) && sceneKey.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
            {
                report.AddError($"PlaceDefinition '{DisplayName}' has an invalid scene key '{sceneKey}'.");
            }

            if (defaultGoverningFaction != null
                && (!definitionsById.TryGetValue(defaultGoverningFaction.Id, out IGameDefinition faction) || faction is not FactionDefinition))
            {
                report.AddError($"PlaceDefinition '{DisplayName}' references default governing faction '{defaultGoverningFaction.Id}', which is not in the configured catalog.");
            }

            if (float.IsNaN(normalizedMapPosition.x)
                || float.IsNaN(normalizedMapPosition.y)
                || float.IsInfinity(normalizedMapPosition.x)
                || float.IsInfinity(normalizedMapPosition.y))
            {
                report.AddError($"PlaceDefinition '{DisplayName}' has non-finite map position metadata.");
            }
        }

        private void OnValidate()
        {
            normalizedMapPosition.x = Mathf.Clamp01(normalizedMapPosition.x);
            normalizedMapPosition.y = Mathf.Clamp01(normalizedMapPosition.y);
        }

        private void ValidateKindCategoryConsistency(DefinitionValidationReport report)
        {
            string expectedCategoryId = PlaceHierarchyUtility.GetExpectedCategoryId(placeKind);
            if (string.IsNullOrWhiteSpace(expectedCategoryId))
            {
                return;
            }

            if (!ClassificationUtility.IsInCategory(primaryCategory, expectedCategoryId))
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' kind '{placeKind}' is not under expected category '{expectedCategoryId}'.");
            }
        }

        private void ValidateSuspiciousHierarchy(DefinitionValidationReport report)
        {
            if (parentPlace == null)
            {
                return;
            }

            if (placeKind == PlaceKind.World)
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' is a world with a parent place.");
            }

            if (parentPlace.PlaceKind == PlaceKind.Building
                && (placeKind == PlaceKind.Nation || placeKind == PlaceKind.Region || placeKind == PlaceKind.Settlement))
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' has suspicious hierarchy: {placeKind} inside Building.");
            }

            if (parentPlace.PlaceKind == PlaceKind.Interior
                && (placeKind == PlaceKind.World || placeKind == PlaceKind.Nation || placeKind == PlaceKind.Region || placeKind == PlaceKind.Settlement))
            {
                report.AddWarning($"PlaceDefinition '{DisplayName}' has suspicious hierarchy: {placeKind} inside Interior.");
            }
        }
    }
}
