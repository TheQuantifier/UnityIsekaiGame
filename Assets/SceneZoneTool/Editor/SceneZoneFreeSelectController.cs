using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SceneZoneTool.Editor
{
    public enum SceneZonePaintMode
    {
        PolygonPoints = 0,
        CenterPoint = 1,
        MovePoints = 2,
        NameZone = 3,
        SelectZone = 4
    }

    /// <summary>Standalone Scene View free-select controller. No project-specific types are used here.</summary>
    internal sealed partial class SceneZoneFreeSelectController : ScriptableObject
    {
        private const float CloseHandlePixels = 14f;
        private readonly Color polygonToolColor = new Color(0.25f, 0.65f, 1f, 1f);
        private readonly Color centerToolColor = new Color(1f, 0.55f, 0.15f, 1f);
        private readonly Color inactivePointColor = new Color(0.65f, 0.9f, 1f, 1f);
        private readonly Color selectedZoneColor = new Color(1f, 0.72f, 0.08f, 1f);
        private readonly Color hoveredZoneColor = new Color(1f, 0.92f, 0.38f, 1f);

        [SerializeField] private SceneZoneAsset zone;
        [SerializeField] private SceneZoneLayerAsset zoneLayer;
        [SerializeField] private SceneZonePaintMode paintMode = SceneZonePaintMode.PolygonPoints;
        [SerializeField] private float displayHeight;
        [SerializeField] private bool showAdvancedOptions;
        [SerializeField] private bool drawing;
        [SerializeField] private bool pendingNewZone;
        [SerializeField] private bool dragToDraw = true;
        [SerializeField] private bool showVisibleZones;
        [SerializeField, Min(0.25f)] private float freehandSpacing = 3f;

        private Vector2 visibleZonesScroll;

        private readonly List<Vector3> draftPoints = new List<Vector3>();
        private readonly List<SceneZoneAsset> layerZones = new List<SceneZoneAsset>();
        private readonly List<SceneZoneLayerAsset> sceneLayers = new List<SceneZoneLayerAsset>();
        private readonly HashSet<SceneZoneAsset> hiddenZones = new HashSet<SceneZoneAsset>();
        private Vector3 hoverPoint;
        private bool hasHover;
        private bool hoveringReusablePoint;
        private bool dragging;
        private int movingDraftPointIndex = -1;
        private bool movingPendingCircleCenter;
        private bool definingCircleRadius;
        private bool replacingCircle;
        private Vector3 pendingCircleCenter;
        private SceneZoneAsset blockedBy;
        private string overlapError;
        private readonly List<Vector3> overlapHighlightSegments = new List<Vector3>();
        private readonly Dictionary<SceneZoneAsset, string> zoneRenameDrafts = new Dictionary<SceneZoneAsset, string>();
        private int movingPointIndex = -1;
        private Vector3[] movingPointDraft = Array.Empty<Vector3>();
        private bool insertingPoint;
        private Texture2D selectHandIcon;
        private Texture2D yellowCircleOutlineIcon;
        private Texture2D bluePolygonOutlineIcon;
        private Texture2D textCursorIcon;
        private GUIStyle zoneNameStyle;
        private SceneView owningSceneView;
        private bool overlayVisible;

        internal static SceneZoneFreeSelectController CreateOverlayController()
        {
            SceneZoneFreeSelectController controller = CreateInstance<SceneZoneFreeSelectController>();
            controller.hideFlags = HideFlags.HideAndDontSave;
            return controller;
        }

        private void OnEnable()
        {
            SceneZoneToolProjectSettings settings = SceneZoneToolProjectSettings.instance;
            displayHeight = settings.DefaultDisplayHeight;
            dragToDraw = settings.AllowDragDrawing;
            freehandSpacing = settings.DragPointSpacing;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.projectChanged += OnProjectChange;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            if (zoneLayer == null) RestorePreferredLayer();
            RefreshLayerZones();
            if (zone == null) zone = layerZones.FirstOrDefault();
            InitializeDefaultVisibility();
        }

        private void OnProjectChange()
        {
            if (zoneLayer != null && !AssetDatabase.Contains(zoneLayer)) zoneLayer = null;
            RefreshLayerZones();
            if (!pendingNewZone && zone != null && !layerZones.Contains(zone)) zone = layerZones.FirstOrDefault();
            if (zone != null) hiddenZones.Remove(zone);
            SceneView.RepaintAll();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            CancelPendingZone();
            zoneLayer = SceneZoneToolProjectSettings.instance.LoadOrDiscoverLayerForActiveScene();
            RefreshLayerZones();
            zone = layerZones.FirstOrDefault();
            InitializeDefaultVisibility();
            ClearOverlapError();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.projectChanged -= OnProjectChange;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            CancelPendingZone();
            owningSceneView = null;
            overlayVisible = false;
        }

        internal void SetOverlayContext(SceneView sceneView, bool visible)
        {
            owningSceneView = sceneView;
            overlayVisible = visible && sceneView != null;
            HandleOverlayVisibilityChanged(overlayVisible);
        }

        private static void Repaint() => SceneView.RepaintAll();

        private static void ShowNotification(GUIContent content)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) sceneView.ShowNotification(content);
            else Debug.LogWarning(content?.text ?? "Scene Zone Tool notification");
        }

        private void SetPoints(IEnumerable<Vector3> values)
        {
            Vector3[] points = (values ?? Array.Empty<Vector3>()).Select(ToStoredPoint).ToArray();
            SerializedObject serialized = new SerializedObject(zone);
            SerializedProperty property = serialized.FindProperty("polygonPoints");
            property.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) property.GetArrayElementAtIndex(i).vector2Value = new Vector2(points[i].x, points[i].z);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
        }

        private void Save()
        {
            if (zone == null || pendingNewZone || !AssetDatabase.Contains(zone)) return;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssetIfDirty(zone);
        }

        internal bool HasPendingZone => pendingNewZone;
        internal SceneZoneAsset PendingZone => pendingNewZone ? zone : null;
        internal SceneZoneAsset ActiveZone => zone;
        internal IReadOnlyList<SceneZoneLayerAsset> SceneLayers => sceneLayers;
        internal Color PersistentSelectionColor => selectedZoneColor;

        internal Color GetDisplayOutlineColor(SceneZoneAsset candidate)
        {
            return candidate != null && candidate == zone && !pendingNewZone
                ? selectedZoneColor
                : zoneLayer != null ? zoneLayer.OutlineColor : Color.white;
        }
        internal SceneZonePaintMode PaintMode => paintMode;
        internal int DraftPointCount => draftPoints.Count;
        internal IReadOnlyList<Vector3> DraftPoints => draftPoints;

        internal bool IsZoneVisible(SceneZoneAsset candidate) => candidate != null && (candidate == zone || !hiddenZones.Contains(candidate));

        internal void SetZoneVisible(SceneZoneAsset candidate, bool visible)
        {
            if (candidate == null || candidate == zone) return;
            if (visible) hiddenZones.Remove(candidate);
            else hiddenZones.Add(candidate);
            SceneView.RepaintAll();
            Repaint();
        }

        internal void ShowAllZones()
        {
            hiddenZones.Clear();
            SceneView.RepaintAll();
            Repaint();
        }

        internal void HideAllZones()
        {
            foreach (SceneZoneLayerAsset layer in sceneLayers)
            foreach (SceneZoneAsset candidate in layer.Zones)
                if (candidate != null && candidate != zone) hiddenZones.Add(candidate);
            SceneView.RepaintAll();
            Repaint();
        }

        internal void ShowActiveLayer()
        {
            hiddenZones.Clear();
            foreach (SceneZoneLayerAsset layer in sceneLayers)
            {
                if (layer == zoneLayer) continue;
                foreach (SceneZoneAsset candidate in layer.Zones)
                    if (candidate != null && candidate != zone) hiddenZones.Add(candidate);
            }
            if (zoneLayer != null)
                foreach (SceneZoneAsset candidate in zoneLayer.Zones)
                    if (candidate != null) hiddenZones.Remove(candidate);
            if (zone != null) hiddenZones.Remove(zone);
            SceneView.RepaintAll();
            Repaint();
        }

        internal void InitializeDefaultVisibility() => ShowActiveLayer();

        internal bool BeginRedrawingSelectedPolygon()
        {
            if (zone == null || pendingNewZone || zone.Shape != SceneZoneShape.Polygon) return false;
            paintMode = SceneZonePaintMode.PolygonPoints;
            BeginDrawing(clearExisting: true);
            return true;
        }

        internal bool BeginReplacingSelectedZone()
        {
            if (zone == null || pendingNewZone) return false;
            Tools.current = Tool.None;
            if (zone.Shape == SceneZoneShape.Polygon) return BeginRedrawingSelectedPolygon();
            drawing = false;
            replacingCircle = true;
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            paintMode = SceneZonePaintMode.CenterPoint;
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
            return true;
        }

        internal bool TryApplyPolygonPoints(IEnumerable<Vector3> values)
        {
            if (zone == null || zone.Shape != SceneZoneShape.Polygon) return false;
            Vector3[] points = (values ?? Array.Empty<Vector3>()).Select(ToStoredPoint).ToArray();
            if (!SceneZoneOverlap.ValidateSimplePolygon(points, out string geometryFailure))
            {
                overlapError = $"ERROR: {geometryFailure}";
                return false;
            }
            if (zone.HasCenterPoint && !SceneZoneOverlap.PolygonContains(points, zone.CenterPoint))
            {
                overlapError = "ERROR: Moving these points would exclude the orange center point.";
                return false;
            }
            SceneZoneAsset conflict = FindPolygonConflict(points, includeClosingSegment: true);
            if (conflict != null)
            {
                blockedBy = conflict;
                SetPolygonOverlapError(points, conflict);
                return false;
            }

            Undo.RecordObject(zone, "Move Zone Points");
            SetPoints(points);
            Save();
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
            return true;
        }

        internal bool TryInsertPolygonPointAfter(int pointIndex, Vector3 value)
        {
            if (zone == null || zone.Shape != SceneZoneShape.Polygon) return false;
            if (pointIndex < 0 || pointIndex >= zone.PolygonPoints.Count) return false;
            return TryApplyPolygonPoints(InsertPointAfter(zone.PolygonPoints, pointIndex, ToStoredPoint(value)));
        }

        private static Vector3[] InsertPointAfter(IReadOnlyList<Vector3> points, int pointIndex, Vector3 value)
        {
            List<Vector3> inserted = new List<Vector3>((points?.Count ?? 0) + 1);
            if (points == null) return inserted.ToArray();
            for (int i = 0; i < points.Count; i++)
            {
                inserted.Add(points[i]);
                if (i == pointIndex) inserted.Add(value);
            }
            return inserted.ToArray();
        }

        private static Vector3 DefaultInsertedPoint(IReadOnlyList<Vector3> points, int pointIndex)
        {
            if (points == null || points.Count == 0 || pointIndex < 0 || pointIndex >= points.Count) return Vector3.zero;
            Vector3 current = points[pointIndex];
            Vector3 next = points[(pointIndex + 1) % points.Count];
            return ToStoredPoint((current + next) * 0.5f);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // Assets intentionally store only world X/Z. The editor plane height is visual authoring
        // state and never becomes part of zone identity, persistence, or containment tests.
        private static Vector3 ToStoredPoint(Vector3 point) => new Vector3(RoundToThreeDecimals(point.x), 0f, RoundToThreeDecimals(point.z));

        private static float RoundToThreeDecimals(float value) => Mathf.Round(value * 1000f) / 1000f;

        private Vector3 ToDisplayPoint(Vector3 storedPoint)
        {
            return new Vector3(storedPoint.x, displayHeight, storedPoint.z);
        }

        private void RestorePreferredLayer()
        {
            zoneLayer = SceneZoneToolProjectSettings.instance.LoadOrDiscoverLayerForActiveScene();
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

        private static string Sanitize(string value) => new string((value ?? "zone").ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
    }

}
