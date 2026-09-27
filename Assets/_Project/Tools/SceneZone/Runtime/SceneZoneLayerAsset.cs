using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneZoneTool
{
    /// <summary>
    /// A reusable authoring layer for mutually exclusive map regions. Boundaries on different
    /// layers may overlap, which supports nesting a settlement layer inside a regional layer.
    /// </summary>
    [CreateAssetMenu(fileName = "zones", menuName = "Zone Boundary Tool/Zone Layer")]
    public sealed class SceneZoneLayerAsset : ScriptableObject
    {
        [SerializeField] private string layerId;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneGuid;
        [SerializeField] private string scenePath;
        [SerializeField] private string sceneName;
        [SerializeField] private Color outlineColor = new Color(1f, 0.7f, 0.15f, 0.8f);
        [SerializeField] private bool preventOverlap = true;
        [SerializeField] private SceneZoneAsset[] zones = Array.Empty<SceneZoneAsset>();

        public string LayerId => layerId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string SceneGuid => sceneGuid ?? string.Empty;
        public string ScenePath => scenePath ?? string.Empty;
        public string SceneName => sceneName ?? string.Empty;
        public bool HasSceneBinding => !string.IsNullOrWhiteSpace(sceneGuid) || !string.IsNullOrWhiteSpace(scenePath) || !string.IsNullOrWhiteSpace(sceneName);
        public Color OutlineColor => outlineColor;
        public bool PreventOverlap => preventOverlap;
        public IReadOnlyList<SceneZoneAsset> Zones => zones ?? Array.Empty<SceneZoneAsset>();
        public IReadOnlyList<SceneZoneAsset> Boundaries => Zones;

        public void Configure(string id, string authoredDisplayName, Color color, bool disallowOverlap = true)
        {
            layerId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(authoredDisplayName) ? layerId : authoredDisplayName.Trim();
            outlineColor = color;
            preventOverlap = disallowOverlap;
        }

        public void BindToScene(string guid, string path, string authoredSceneName)
        {
            sceneGuid = Normalize(guid);
            scenePath = Normalize(path);
            sceneName = Normalize(authoredSceneName);
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(layerId)) { failure = "Layer ID is required."; return false; }
            SceneZoneAsset[] values = (zones ?? Array.Empty<SceneZoneAsset>()).Where(value => value != null).ToArray();
            if (values.Any(value => value.BoundaryLayer != this)) { failure = "Every zone stored by the layer must reference that layer."; return false; }
            if (values.GroupBy(value => value.BoundaryId, StringComparer.Ordinal).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
            {
                failure = "Every zone in the layer needs a unique, non-empty boundary ID.";
                return false;
            }
            if (values.GroupBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
            {
                failure = "Every zone in the layer needs a unique, non-empty display name.";
                return false;
            }
            foreach (SceneZoneAsset zone in values)
            {
                if (!zone.Validate(out string zoneFailure))
                {
                    failure = $"Zone '{zone.DisplayName}' is invalid: {zoneFailure}";
                    return false;
                }
            }
            if (preventOverlap)
            {
                for (int first = 0; first < values.Length; first++)
                for (int second = first + 1; second < values.Length; second++)
                {
                    if (!SceneZoneOverlap.Overlaps(values[first], values[second])) continue;
                    failure = $"Zones '{values[first].DisplayName}' and '{values[second].DisplayName}' overlap in a non-overlapping layer.";
                    return false;
                }
            }
            failure = string.Empty;
            return true;
        }

        public bool TryGetZone(string boundaryId, out SceneZoneAsset zone)
        {
            string normalized = Normalize(boundaryId);
            zone = (zones ?? Array.Empty<SceneZoneAsset>()).FirstOrDefault(value => value != null && string.Equals(value.BoundaryId, normalized, StringComparison.Ordinal));
            return zone != null;
        }

        public SceneZoneAsset GetZone(string boundaryId)
        {
            TryGetZone(boundaryId, out SceneZoneAsset zone);
            return zone;
        }

        public SceneZoneAsset FindZoneByName(string zoneName)
        {
            string normalized = Normalize(zoneName);
            return (zones ?? Array.Empty<SceneZoneAsset>()).FirstOrDefault(value => value != null && string.Equals(value.DisplayName, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<Vector2> GetZonePoints(string boundaryId)
        {
            return TryGetZone(boundaryId, out SceneZoneAsset zone) ? zone.PolygonPoints2D.ToArray() : Array.Empty<Vector2>();
        }

        public IReadOnlyList<Vector3> GetZoneWorldPlanePoints(string boundaryId)
        {
            return TryGetZone(boundaryId, out SceneZoneAsset zone) ? zone.PolygonPoints.ToArray() : Array.Empty<Vector3>();
        }

        private void OnValidate()
        {
            layerId = Normalize(layerId);
            displayName = displayName?.Trim();
            sceneGuid = Normalize(sceneGuid);
            scenePath = Normalize(scenePath);
            sceneName = Normalize(sceneName);
            zones = (zones ?? Array.Empty<SceneZoneAsset>()).Where(value => value != null).Distinct().ToArray();
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
