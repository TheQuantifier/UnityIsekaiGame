using System;
using System.Linq;
using SceneZoneTool;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Editor
{
    /// <summary>Thin project adapter; the reusable free-select painter remains game agnostic.</summary>
    public sealed class SpatialTerritoryBoundaryBindingWindow : EditorWindow
    {
        [SerializeField] private SceneZoneAsset zoneBoundary;
        [SerializeField] private PlaceDefinition place;
        [SerializeField] private string locationId = "location.prototype.town";
        [SerializeField] private string sceneKey = "scene.prototype";
        [SerializeField] private SpatialBoundaryOperation operation = SpatialBoundaryOperation.Include;
        [SerializeField] private int priority;
        [SerializeField] private bool showInGame;

        [MenuItem("Tools/Unity Isekai Game/World/Bind Zone To Place")]
        public static void Open()
        {
            SpatialTerritoryBoundaryBindingWindow window = GetWindow<SpatialTerritoryBoundaryBindingWindow>("Bind Territory Boundary");
            if (Selection.activeObject is SceneZoneAsset selected) window.zoneBoundary = selected;
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("This adapter assigns a standalone two-dimensional scene zone to this game's logical Place and Location. Government ownership is resolved at runtime.", MessageType.Info);
            zoneBoundary = (SceneZoneAsset)EditorGUILayout.ObjectField("Zone Boundary", zoneBoundary, typeof(SceneZoneAsset), false);
            place = (PlaceDefinition)EditorGUILayout.ObjectField("Place", place, typeof(PlaceDefinition), false);
            locationId = EditorGUILayout.TextField("Runtime Location ID", locationId);
            sceneKey = EditorGUILayout.TextField("Runtime Scene Key", sceneKey);
            operation = (SpatialBoundaryOperation)EditorGUILayout.EnumPopup("Operation", operation);
            priority = EditorGUILayout.IntField("Overlap Priority", priority);
            showInGame = EditorGUILayout.Toggle("Show In Game", showInGame);

            using (new EditorGUI.DisabledScope(zoneBoundary == null || place == null || string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(sceneKey)))
            {
                if (GUILayout.Button("Create Place Boundary Binding")) CreateBinding();
            }
            if (GUILayout.Button("Open Scene Zone Tools")) EditorApplication.ExecuteMenuItem("Tools/Scene Zone Tools/Open Authoring Overlay");
        }

        private void CreateBinding()
        {
            string defaultName = $"{place.Id}.{zoneBoundary.ZoneId}".Replace('.', '_');
            string path = EditorUtility.SaveFilePanelInProject("Create Spatial Territory Binding", defaultName, "asset", "Choose where to save the game-specific binding.", "Assets/_Project/Content/World/SpatialBoundaries");
            if (string.IsNullOrWhiteSpace(path)) return;
            SpatialTerritoryBoundaryDefinition binding = CreateInstance<SpatialTerritoryBoundaryDefinition>();
            string suffix = Sanitize(place.Id.Replace("place.", string.Empty, StringComparison.Ordinal));
            binding.DevelopmentConfigure($"spatial-boundary.{suffix}.{Sanitize(zoneBoundary.ZoneId)}", $"{place.DisplayName} Boundary", zoneBoundary, place, locationId, sceneKey, operation, priority, showInGame);
            AssetDatabase.CreateAsset(binding, path);
            AssetDatabase.SaveAssets();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Selection.activeObject = binding;
        }

        private static string Sanitize(string value)
        {
            return new string((value ?? "boundary").ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        }
    }

    [CustomEditor(typeof(SpatialTerritoryBoundaryDefinition))]
    public sealed class SpatialTerritoryBoundaryDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SpatialTerritoryBoundaryDefinition definition = (SpatialTerritoryBoundaryDefinition)target;
            EditorGUILayout.Space();
            if (definition.ZoneBoundary != null && GUILayout.Button("Select Standalone Zone")) Selection.activeObject = definition.ZoneBoundary;
            if (GUILayout.Button("Open Scene Zone Tools")) EditorApplication.ExecuteMenuItem("Tools/Scene Zone Tools");
        }
    }

    public static class SpatialTerritoryBoundaryPrototypeAuthoring
    {
        private const string BindingFolder = "Assets/_Project/Content/World/SpatialBoundaries/Bindings";
        private const string LayerFolder = "Assets/_Project/Content/World/SpatialBoundaries/Layers";
        private const string LegacyOutskirtsGeometryPath = "Assets/_Project/Content/World/SpatialBoundaries/Geometry/PrototypeOutskirtsBoundary.asset";
        private const string LegacyTownGeometryPath = "Assets/_Project/Content/World/SpatialBoundaries/Geometry/PrototypeTownBoundary.asset";
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/Unity Isekai Game/World/Create Prototype Boundary Defaults")]
        public static void GeneratePrototypeDefaults()
        {
            EnsureFolder(BindingFolder);
            EnsureFolder(LayerFolder);
            PlaceDefinition town = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeTownPlace.asset");
            PlaceDefinition outskirts = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeOutskirtsPlace.asset");
            if (town == null || outskirts == null) throw new InvalidOperationException("Prototype town/outskirts Place assets are missing.");

            SceneZoneLayerAsset regionalLayer = UpsertLayer($"{LayerFolder}/RegionalBoundaries.asset", "zone-layer.regions", "Regional Boundaries", new Color(0.35f, 0.8f, 0.4f, 0.85f));
            SceneZoneLayerAsset settlementLayer = UpsertLayer($"{LayerFolder}/SettlementBoundaries.asset", "zone-layer.settlements", "Settlement Boundaries", new Color(0.2f, 0.75f, 1f, 0.85f));
            SceneZoneAsset regionalGeometry = UpsertEmbeddedZone(
                regionalLayer,
                "zone.prototype-outskirts",
                "Prototype Outskirts Boundary",
                new[]
                {
                    new Vector3(-500f, 0f, -500f),
                    new Vector3(500f, 0f, -500f),
                    new Vector3(500f, 0f, 500f),
                    new Vector3(-500f, 0f, 500f)
                });
            SceneZoneAsset townGeometry = UpsertEmbeddedZone(
                settlementLayer,
                "zone.prototype-town",
                "Prototype Town Boundary",
                new[]
                {
                    new Vector3(-60f, 0f, -20f),
                    new Vector3(40f, 0f, -20f),
                    new Vector3(40f, 0f, 120f),
                    new Vector3(-60f, 0f, 120f)
                });

            UpsertBinding($"{BindingFolder}/PrototypeOutskirtsSpatialBoundary.asset", "spatial-boundary.prototype.outskirts", "Prototype Outskirts Spatial Boundary", regionalGeometry, outskirts, "location.prototype.wilderness-ring", 0);
            UpsertBinding($"{BindingFolder}/PrototypeTownSpatialBoundary.asset", "spatial-boundary.prototype.town", "Prototype Town Spatial Boundary", townGeometry, town, "location.prototype.town", 100);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(LegacyOutskirtsGeometryPath);
            AssetDatabase.DeleteAsset(LegacyTownGeometryPath);
            if (AssetDatabase.IsValidFolder("Assets/_Project/Content/World/SpatialBoundaries/Geometry"))
            {
                string[] remaining = AssetDatabase.FindAssets(string.Empty, new[] { "Assets/_Project/Content/World/SpatialBoundaries/Geometry" });
                if (remaining.Length == 0) AssetDatabase.DeleteAsset("Assets/_Project/Content/World/SpatialBoundaries/Geometry");
            }
            AssetDatabase.Refresh();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Debug.Log("Created standalone Prototype town/outskirts zones and their game-specific Place bindings. Use the free-select painter to replace the rectangular defaults.");
        }

        private static SceneZoneLayerAsset UpsertLayer(string path, string id, string displayName, Color color)
        {
            SceneZoneLayerAsset asset = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SceneZoneLayerAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, displayName, color);
            asset.BindToScene(AssetDatabase.AssetPathToGUID(PrototypeScenePath), PrototypeScenePath, "PrototypeScene");
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SceneZoneAsset UpsertEmbeddedZone(SceneZoneLayerAsset layer, string id, string displayName, Vector3[] points)
        {
            SceneZoneAsset asset = layer.GetZone(id);
            if (asset == null)
            {
                SceneZoneAsset[] embedded = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(layer)).OfType<SceneZoneAsset>().ToArray();
                asset = embedded.FirstOrDefault(value => string.Equals(value.ZoneId, id, StringComparison.Ordinal))
                    ?? embedded.FirstOrDefault(value => string.Equals(value.DisplayName, displayName, StringComparison.Ordinal));
            }
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SceneZoneAsset>();
                asset.name = displayName;
                AssetDatabase.AddObjectToAsset(asset, layer);
                asset.Configure(id, displayName, SceneZoneShape.Polygon, points, authoredLayer: layer);
            }
            else
            {
                // Seed missing defaults without replacing geometry later authored by the painter.
                string preservedName = string.IsNullOrWhiteSpace(asset.DisplayName) ? displayName : asset.DisplayName;
                if (asset.Shape == SceneZoneShape.Circle)
                {
                    asset.Configure(
                        id,
                        preservedName,
                        SceneZoneShape.Circle,
                        center: asset.CircleCenter,
                        radius: asset.CircleRadius,
                        authoredLayer: layer,
                        useAuthoredCenter: asset.HasCenterPoint,
                        authoredCenter: asset.CenterPoint);
                }
                else
                {
                    Vector3[] preservedPoints = asset.PolygonPoints.ToArray();
                    asset.Configure(
                        id,
                        preservedName,
                        SceneZoneShape.Polygon,
                        preservedPoints.Length == 0 ? points : preservedPoints,
                        authoredLayer: layer,
                        useAuthoredCenter: asset.HasCenterPoint,
                        authoredCenter: asset.CenterPoint);
                }
            }
            EditorUtility.SetDirty(asset);
            SetLayerZones(layer, layer.Zones.Append(asset));
            return asset;
        }

        private static void SetLayerZones(SceneZoneLayerAsset layer, System.Collections.Generic.IEnumerable<SceneZoneAsset> values)
        {
            SceneZoneAsset[] zones = (values ?? Array.Empty<SceneZoneAsset>()).Where(value => value != null).Distinct().ToArray();
            SerializedObject serialized = new SerializedObject(layer);
            SerializedProperty property = serialized.FindProperty("zones");
            property.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layer);
        }

        private static void UpsertBinding(string path, string id, string displayName, SceneZoneAsset geometry, PlaceDefinition place, string locationId, int priority)
        {
            SpatialTerritoryBoundaryDefinition asset = AssetDatabase.LoadAssetAtPath<SpatialTerritoryBoundaryDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SpatialTerritoryBoundaryDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.DevelopmentConfigure(id, displayName, geometry, place, locationId, "scene.prototype", SpatialBoundaryOperation.Include, priority);
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cursor = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cursor}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cursor, parts[i]);
                cursor = next;
            }
        }
    }
}
