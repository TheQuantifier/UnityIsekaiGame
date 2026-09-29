using System;
using System.Collections.Generic;
using SceneZoneTool;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.WorldLocations
{
    public enum SpatialBoundaryOperation
    {
        Include = 0,
        Exclude = 1
    }

    /// <summary>
    /// Game-specific binding from a standalone two-dimensional zone to a logical Place/Location.
    /// Political ownership remains live GovernmentRuntime data, so conquest does not rewrite geometry.
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialTerritoryBoundary", menuName = "Unity Isekai Game/World/Spatial Territory Boundary Binding")]
    public sealed class SpatialTerritoryBoundaryDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string boundaryId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private SceneZoneAsset zoneBoundary;
        [SerializeField] private PlaceDefinition place;
        [SerializeField] private string locationId;
        [SerializeField] private string sceneKey;
        [SerializeField] private SpatialBoundaryOperation operation = SpatialBoundaryOperation.Include;
        [SerializeField] private int priority;
        [SerializeField] private bool enabledForResolution = true;
        [SerializeField] private bool showInGame;
        [SerializeField] private int version = 1;

        public string Id => boundaryId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SceneZoneAsset ZoneBoundary => zoneBoundary;
        public PlaceDefinition Place => place;
        public string PlaceId => place == null ? string.Empty : place.Id;
        public string LocationId => locationId ?? string.Empty;
        public string SceneKey => sceneKey ?? string.Empty;
        public SpatialBoundaryOperation Operation => operation;
        public int Priority => priority;
        public bool EnabledForResolution => enabledForResolution;
        public bool ShowInGame => showInGame;
        public Color MapColor => zoneBoundary == null ? Color.cyan : zoneBoundary.LineColor;
        public float MapLineWidth => zoneBoundary == null ? 0.2f : zoneBoundary.LineWidth;
        public int Version => version;

        public bool Contains(Vector3 worldPosition) => enabledForResolution && zoneBoundary != null && zoneBoundary.Contains(worldPosition);
        public float ApproximateHorizontalArea() => zoneBoundary == null ? float.PositiveInfinity : zoneBoundary.ApproximateHorizontalArea();

        public void DevelopmentConfigure(
            string id,
            string authoredDisplayName,
            SceneZoneAsset sourceBoundary,
            PlaceDefinition targetPlace,
            string targetLocationId,
            string targetSceneKey,
            SpatialBoundaryOperation boundaryOperation = SpatialBoundaryOperation.Include,
            int resolutionPriority = 0,
            bool renderInGame = false)
        {
            boundaryId = N(id);
            displayName = string.IsNullOrWhiteSpace(authoredDisplayName) ? boundaryId : authoredDisplayName.Trim();
            zoneBoundary = sourceBoundary;
            place = targetPlace;
            locationId = N(targetLocationId);
            sceneKey = N(targetSceneKey);
            operation = boundaryOperation;
            priority = resolutionPriority;
            enabledForResolution = true;
            showInGame = renderInGame;
            version = Math.Max(1, version);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Spatial boundary binding '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("spatial-boundary.", StringComparison.Ordinal)) report.AddWarning($"Spatial boundary binding '{Id}' should use the 'spatial-boundary.' namespace prefix.");
            if (zoneBoundary == null) report.AddError($"Spatial boundary binding '{DisplayName}' is missing its standalone zone asset.");
            else if (!zoneBoundary.Validate(out string failure)) report.AddError($"Spatial boundary binding '{DisplayName}' has invalid geometry: {failure}");
            if (place == null || string.IsNullOrWhiteSpace(place.Id)) report.AddError($"Spatial boundary binding '{DisplayName}' must target a Place definition.");
            else if (definitionsById == null || !definitionsById.TryGetValue(place.Id, out IGameDefinition knownPlace) || knownPlace is not PlaceDefinition)
                report.AddError($"Spatial boundary binding '{DisplayName}' targets Place '{place.Id}', which is not in the configured catalog.");
            if (string.IsNullOrWhiteSpace(locationId) || !locationId.StartsWith("location.", StringComparison.Ordinal)) report.AddError($"Spatial boundary binding '{DisplayName}' must target a normalized runtime Location ID.");
            if (string.IsNullOrWhiteSpace(sceneKey) || !sceneKey.StartsWith("scene.", StringComparison.Ordinal)) report.AddError($"Spatial boundary binding '{DisplayName}' must target a normalized runtime Scene key.");
        }

        private void OnValidate()
        {
            boundaryId = N(boundaryId);
            displayName = displayName?.Trim();
            locationId = N(locationId);
            sceneKey = N(sceneKey);
            version = Math.Max(1, version);
        }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
