using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneZoneTool.Editor.Tests
{
    public sealed class SceneZoneEditorWorkflowTests
    {
        private const string TemporaryFolder = "Assets/_Project/Tools/SceneZone/Tests/TempGenerated";
        private SceneZoneLayerAsset previousPreferredLayer;
        private string temporaryLayerGuid;

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(TemporaryFolder);
            previousPreferredLayer = SceneZoneToolProjectSettings.instance.LoadLayerForActiveScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(temporaryLayerGuid))
                SceneZoneToolProjectSettings.instance.RemoveLayer(temporaryLayerGuid);
            AssetDatabase.DeleteAsset(TemporaryFolder);
            AssetDatabase.Refresh();
            if (previousPreferredLayer != null)
                SceneZoneToolProjectSettings.instance.SetLayerForActiveScene(previousPreferredLayer);
            temporaryLayerGuid = string.Empty;
        }

        [Test]
        public void PreferredLayer_FollowsAssetGuidAfterFileMove()
        {
            SceneZoneLayerAsset layer = CreateLayer("Original.asset");
            SceneZoneToolProjectSettings.instance.SetLayerForActiveScene(layer);

            string moveFailure = AssetDatabase.MoveAsset($"{TemporaryFolder}/Original.asset", $"{TemporaryFolder}/Moved.asset");

            Assert.That(moveFailure, Is.Empty);
            Assert.That(SceneZoneToolProjectSettings.instance.LoadLayerForActiveScene(), Is.SameAs(layer));
        }

        [Test]
        public void NewZoneDraft_RemainsTransientAndIsDiscardedWithController()
        {
            SceneZoneLayerAsset layer = CreateLayer("Drafts.asset");
            SceneZoneFreeSelectController controller = SceneZoneFreeSelectController.CreateOverlayController();
            controller.UseLayer(layer);

            controller.BeginNewZone(SceneZoneShape.Polygon);
            SceneZoneAsset firstDraft = controller.PendingZone;
            Assert.That(controller.HasPendingZone, Is.True);
            Assert.That(firstDraft, Is.Not.Null);
            Assert.That(layer.Zones, Is.Empty);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(layer)).OfType<SceneZoneAsset>(), Is.Empty);

            controller.BeginNewZone(SceneZoneShape.Circle);
            Assert.That(firstDraft == null, Is.True, "Starting another tool destroys the prior transient draft.");
            Assert.That(controller.HasPendingZone, Is.True);
            Assert.That(layer.Zones, Is.Empty);

            Object.DestroyImmediate(controller);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(layer)).OfType<SceneZoneAsset>(), Is.Empty);
        }

        [Test]
        public void PolygonCoordinates_RoundToThreeDecimalsWithoutSnapping()
        {
            SceneZoneLayerAsset layer = CreateLayer("Rounding.asset");
            SceneZoneFreeSelectController controller = SceneZoneFreeSelectController.CreateOverlayController();
            controller.UseLayer(layer);
            controller.BeginNewZone(SceneZoneShape.Polygon);
            controller.AddDraft(new Vector3(12.3454f, 42f, -12.9874f));

            Vector3 saved = controller.DraftPoints.Single();
            Assert.That(saved.x, Is.EqualTo(12.345f).Within(0.0001f));
            Assert.That(saved.y, Is.Zero);
            Assert.That(saved.z, Is.EqualTo(-12.987f).Within(0.0001f));
            Object.DestroyImmediate(controller);
        }

        [Test]
        public void DistantPolygon_DoesNotConflictWithExistingPolygon()
        {
            SceneZoneLayerAsset layer = CreateLayer("Distant.asset");
            SceneZoneFreeSelectController controller = SceneZoneFreeSelectController.CreateOverlayController();
            controller.UseLayer(layer);

            controller.BeginNewZone(SceneZoneShape.Polygon);
            controller.AddDraft(new Vector3(-20f, 0f, -20f));
            controller.AddDraft(new Vector3(20f, 0f, -20f));
            controller.AddDraft(new Vector3(20f, 0f, 20f));
            controller.AddDraft(new Vector3(-20f, 0f, 20f));
            controller.CommitDraft();

            controller.BeginNewZone(SceneZoneShape.Polygon);
            controller.AddDraft(new Vector3(700f, 0f, -450f));
            controller.AddDraft(new Vector3(740f, 0f, -450f));
            controller.AddDraft(new Vector3(740f, 0f, -410f));
            controller.AddDraft(new Vector3(700f, 0f, -410f));
            controller.CommitDraft();

            Assert.That(controller.HasPendingZone, Is.False);
            Assert.That(layer.Zones.Count, Is.EqualTo(2));
            Assert.That(layer.Zones.All(zone => zone.IsUsable), Is.True);
            Object.DestroyImmediate(controller);
        }

        [Test]
        public void SelfIntersectionError_UsesRoundedSegmentCoordinates()
        {
            Vector3[] crossing =
            {
                new Vector3(1.2f, 0f, 1.4f),
                new Vector3(8.7f, 0f, 8.6f),
                new Vector3(1.1f, 0f, 8.8f),
                new Vector3(8.9f, 0f, 1.2f)
            };

            Assert.That(SceneZoneOverlap.ValidateSimplePolygon(crossing, out string failure), Is.False);
            Assert.That(failure, Does.Contain("segment [<1,1>,<9,9>]").And.Contain("segment [<1,9>,<9,1>]"));
        }

        [Test]
        public void OverlayCommands_CreateEditRenameSelectAndDeleteZones()
        {
            SceneZoneLayerAsset layer = CreateLayer("Commands.asset");
            SceneZoneFreeSelectController controller = SceneZoneFreeSelectController.CreateOverlayController();
            controller.UseLayer(layer);

            // Blue polygon button, click placement, Undo Point, and Close.
            controller.BeginNewZone(SceneZoneShape.Polygon);
            Assert.That(controller.PaintMode, Is.EqualTo(SceneZonePaintMode.PolygonPoints));
            Assert.That(controller.PendingZone.Shape, Is.EqualTo(SceneZoneShape.Polygon));
            Assert.That(controller.DraftPointCount, Is.Zero, "Choosing the polygon tool must not create a point from the toolbar click.");
            controller.AddDraft(new Vector3(0f, 12f, 0f));
            controller.AddDraft(new Vector3(10f, -4f, 0f));
            controller.AddDraft(new Vector3(10f, 200f, 10f));
            controller.AddDraft(new Vector3(0f, 0f, 10f));
            Assert.That(controller.DraftPointCount, Is.EqualTo(4));
            Assert.That(controller.DraftPoints.All(point => point.y == 0f), Is.True, "Authored height must not be stored.");
            controller.RemoveLast();
            Assert.That(controller.DraftPointCount, Is.EqualTo(3));
            controller.AddDraft(new Vector3(0f, -100f, 10f));
            controller.CommitDraft();

            SceneZoneAsset polygon = controller.ActiveZone;
            Assert.That(controller.HasPendingZone, Is.False);
            Assert.That(polygon, Is.Not.Null);
            Assert.That(polygon.IsUsable, Is.True);
            Assert.That(polygon.PolygonPoints.Count, Is.EqualTo(4));
            Assert.That(layer.Zones.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.Contains(polygon), Is.True);

            // Redrawing replaces geometry without replacing the zone asset referenced by gameplay.
            Vector3[] originalPoints = polygon.PolygonPoints.ToArray();
            Assert.That(controller.BeginRedrawingSelectedPolygon(), Is.True);
            Assert.That(controller.HasPendingZone, Is.False);
            Assert.That(controller.ActiveZone, Is.SameAs(polygon));
            Assert.That(controller.DraftPointCount, Is.Zero);
            controller.AddDraft(new Vector3(-4f, 0f, -4f));
            controller.AddDraft(new Vector3(14f, 0f, -4f));
            controller.AddDraft(new Vector3(14f, 0f, 14f));
            controller.CancelDrawing();
            Assert.That(polygon.PolygonPoints, Is.EqualTo(originalPoints), "Cancel must preserve the saved outline.");

            Assert.That(controller.BeginRedrawingSelectedPolygon(), Is.True);
            controller.AddDraft(new Vector3(-4f, 0f, -4f));
            controller.AddDraft(new Vector3(14f, 0f, -4f));
            controller.AddDraft(new Vector3(14f, 0f, 14f));
            controller.AddDraft(new Vector3(-4f, 0f, 14f));
            controller.CommitDraft();
            Assert.That(controller.ActiveZone, Is.SameAs(polygon));
            Assert.That(layer.Zones.Single(), Is.SameAs(polygon));
            Assert.That(polygon.PolygonPoints[0], Is.EqualTo(new Vector3(-4f, 0f, -4f)));

            // Starting another outline and pressing Cancel discards it.
            controller.BeginNewZone(SceneZoneShape.Polygon);
            controller.AddDraft(new Vector3(25f, 0f, 0f));
            controller.AddDraft(new Vector3(30f, 0f, 0f));
            controller.CancelDrawing();
            Assert.That(controller.HasPendingZone, Is.False);
            Assert.That(layer.Zones.Count, Is.EqualTo(1));

            // Yellow circle button and its second-click radius operation.
            controller.BeginNewZone(SceneZoneShape.Circle);
            Assert.That(controller.PaintMode, Is.EqualTo(SceneZonePaintMode.CenterPoint));
            controller.SetCircle(new Vector3(40f, 9f, 0f), 5f);
            SceneZoneAsset circle = controller.ActiveZone;
            Assert.That(circle.Shape, Is.EqualTo(SceneZoneShape.Circle));
            Assert.That(circle.CircleCenter, Is.EqualTo(new Vector3(40f, 0f, 0f)));
            Assert.That(circle.CircleRadius, Is.EqualTo(5f));
            Assert.That(layer.Zones.Count, Is.EqualTo(2));
            Assert.That((Color32)circle.FillColor, Is.Not.EqualTo((Color32)polygon.FillColor), "Zones in one layer need unique saved fill colors.");
            Assert.That(circle.FillColor.a, Is.GreaterThan(0f));

            // Label/rename, zone selection, and mode buttons.
            Assert.DoesNotThrow(() => controller.RenameZone(circle, "East Circle"));
            Assert.That(circle.DisplayName, Is.EqualTo("East Circle"));
            controller.SelectBoundary(polygon);
            Assert.That(controller.ActiveZone, Is.SameAs(polygon));
            controller.SelectPaintMode(SceneZonePaintMode.MovePoints);
            Assert.That(controller.PaintMode, Is.EqualTo(SceneZonePaintMode.MovePoints));

            Vector3[] moved =
            {
                new Vector3(-2f, 77f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, 10f)
            };
            Assert.That(controller.TryApplyPolygonPoints(moved), Is.True);
            Assert.That(polygon.PolygonPoints[0], Is.EqualTo(new Vector3(-2f, 0f, 0f)));
            Assert.That(controller.TryInsertPolygonPointAfter(1, new Vector3(12f, 0f, 5f)), Is.True);
            Assert.That(polygon.PolygonPoints.Count, Is.EqualTo(5));
            Assert.That(polygon.PolygonPoints[1], Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(polygon.PolygonPoints[2], Is.EqualTo(new Vector3(12f, 0f, 5f)), "The inserted point must follow B in polygon order.");
            controller.SelectPaintMode(SceneZonePaintMode.NameZone);
            Assert.That(controller.PaintMode, Is.EqualTo(SceneZonePaintMode.NameZone));
            controller.SelectPaintMode(SceneZonePaintMode.SelectZone);
            Assert.That(controller.PaintMode, Is.EqualTo(SceneZonePaintMode.SelectZone));

            // Zone minus button command.
            controller.DeleteZoneInternal(circle);
            Assert.That(layer.Zones.Count, Is.EqualTo(1));
            Assert.That(layer.Zones.Single(), Is.SameAs(polygon));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(layer)).OfType<SceneZoneAsset>().Count(), Is.EqualTo(1));

            Object.DestroyImmediate(controller);
        }

        private SceneZoneLayerAsset CreateLayer(string fileName)
        {
            string path = $"{TemporaryFolder}/{fileName}";
            SceneZoneLayerAsset layer = ScriptableObject.CreateInstance<SceneZoneLayerAsset>();
            layer.Configure("zone-layer.editor-test", "Editor Test", Color.yellow);
            Scene scene = SceneManager.GetActiveScene();
            string scenePath = scene.path ?? string.Empty;
            layer.BindToScene(string.IsNullOrWhiteSpace(scenePath) ? string.Empty : AssetDatabase.AssetPathToGUID(scenePath), scenePath, scene.name);
            AssetDatabase.CreateAsset(layer, path);
            AssetDatabase.SaveAssets();
            temporaryLayerGuid = AssetDatabase.AssetPathToGUID(path);
            return layer;
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
