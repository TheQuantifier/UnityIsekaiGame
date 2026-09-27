using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SceneZoneTool;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class SpatialTerritoryBoundaryTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string RegionalLayerPath = "Assets/_Project/Content/World/SpatialBoundaries/Layers/RegionalBoundaries.asset";
        private const string SettlementLayerPath = "Assets/_Project/Content/World/SpatialBoundaries/Layers/SettlementBoundaries.asset";
        private readonly List<Object> transients = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object transient in transients) if (transient != null) Object.DestroyImmediate(transient);
            transients.Clear();
        }

        [Test]
        public void StandalonePolygon_FreeSelectShapeIncludesInteriorAndEdge()
        {
            SceneZoneAsset boundary = Geometry("test.square", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f)
            });

            Assert.That(boundary.Contains(new Vector3(5f, 500f, 5f)), Is.True, "Outdoor borders ignore height by default.");
            Assert.That(boundary.Contains(new Vector3(0f, 0f, 5f)), Is.True, "Points directly on a border are inside.");
            Assert.That(boundary.Contains(new Vector3(10.01f, 0f, 5f)), Is.False);
            Assert.That(boundary.ApproximateHorizontalArea(), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void StandaloneCircle_IsStrictlyTwoDimensionalAndIgnoresHeight()
        {
            SceneZoneAsset boundary = ScriptableObject.CreateInstance<SceneZoneAsset>();
            transients.Add(boundary);
            boundary.Configure("test.circle", "Circle", SceneZoneShape.Circle, center: Vector3.zero, radius: 10f);

            Assert.That(boundary.Contains(new Vector3(9f, 0f, 0f)), Is.True);
            Assert.That(boundary.Contains(new Vector3(0f, 3000f, 0f)), Is.True, "Scene height is not stored and is not part of zone containment.");
            Assert.That(boundary.Contains(new Vector3(11f, 0f, 0f)), Is.False);
        }

        [Test]
        public void StandaloneCircleValidation_RejectsNonFiniteGeometry()
        {
            SceneZoneAsset boundary = ScriptableObject.CreateInstance<SceneZoneAsset>();
            transients.Add(boundary);
            boundary.Configure("test.invalid-circle", "Invalid Circle", SceneZoneShape.Circle, center: new Vector3(float.NaN, 0f, 0f), radius: float.PositiveInfinity);

            Assert.That(boundary.IsUsable, Is.False);
            Assert.That(boundary.Validate(out string failure), Is.False);
            Assert.That(failure, Does.Contain("finite"));
        }

        [Test]
        public void StandaloneGeometry_StoresOnlyWorldXAndZ()
        {
            SceneZoneAsset boundary = Geometry("test.two-dimensional", new[]
            {
                new Vector3(1f, -500f, 2f),
                new Vector3(7f, 25f, 2f),
                new Vector3(7f, 9000f, 8f),
                new Vector3(1f, 3f, 8f)
            });

            Assert.That(boundary.PolygonPoints2D, Is.EqualTo(new[]
            {
                new Vector2(1f, 2f), new Vector2(7f, 2f),
                new Vector2(7f, 8f), new Vector2(1f, 8f)
            }));
            Assert.That(boundary.PolygonPoints.All(point => point.y == 0f), Is.True);
            Assert.That(boundary.Contains(new Vector3(4f, -100000f, 5f)), Is.True);
            Assert.That(boundary.Contains(new Vector3(4f, 100000f, 5f)), Is.True);
        }

        [Test]
        public void StandaloneZones_HaveNoCanvasOrAuthoringSizeLimit()
        {
            const float extent = 10_000_000f;
            SceneZoneAsset boundary = Geometry("test.world-scale", new[]
            {
                new Vector3(-extent, 0f, -extent), new Vector3(extent, 0f, -extent),
                new Vector3(extent, 0f, extent), new Vector3(-extent, 0f, extent)
            });

            Assert.That(boundary.PolygonPoints.Count, Is.EqualTo(4));
            Assert.That(boundary.Contains(Vector3.zero), Is.True);
            Assert.That(boundary.Contains(new Vector3(extent - 1f, 100000f, extent - 1f)), Is.True);
            Assert.That(boundary.Contains(new Vector3(extent + 1f, 0f, 0f)), Is.False);
        }

        [Test]
        public void ZoneLayer_StoresItsStableUnitySceneBinding()
        {
            SceneZoneLayerAsset layer = ScriptableObject.CreateInstance<SceneZoneLayerAsset>();
            transients.Add(layer);
            layer.Configure("zone-layer.scene-test", "Scene Test", Color.cyan);
            layer.BindToScene("scene-guid-123", "Assets/_Project/Scenes/TestScene.unity", "TestScene");

            Assert.That(layer.HasSceneBinding, Is.True);
            Assert.That(layer.SceneGuid, Is.EqualTo("scene-guid-123"));
            Assert.That(layer.ScenePath, Is.EqualTo("Assets/_Project/Scenes/TestScene.unity"));
            Assert.That(layer.SceneName, Is.EqualTo("TestScene"));
        }

        [Test]
        public void StandaloneLayer_SharedCoordinatesAreAllowedButAreaOverlapIsDetected()
        {
            SceneZoneLayerAsset layer = ScriptableObject.CreateInstance<SceneZoneLayerAsset>();
            transients.Add(layer);
            layer.Configure("zone-layer.test", "Test Layer", Color.yellow);
            SceneZoneAsset original = Geometry("test.original", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f)
            }, layer);
            SceneZoneAsset adjacentWithSharedCoordinates = Geometry("test.adjacent", new[]
            {
                new Vector3(20f, 0f, 0f), original.PolygonPoints[1],
                original.PolygonPoints[2], new Vector3(20f, 0f, 10f)
            }, layer);
            SceneZoneAsset overlapping = Geometry("test.overlap", new[]
            {
                new Vector3(5f, 0f, 0f), new Vector3(15f, 0f, 0f),
                new Vector3(15f, 0f, 10f), new Vector3(5f, 0f, 10f)
            }, layer);

            Assert.That(original.BoundaryLayer, Is.SameAs(layer));
            Assert.That(SceneZoneOverlap.Overlaps(original, adjacentWithSharedCoordinates), Is.False, "An exactly shared edge has no overlapping area.");
            Assert.That(SceneZoneOverlap.Overlaps(original, overlapping), Is.True);
            Assert.That(SceneZoneOverlap.FindPolygonConflicts(adjacentWithSharedCoordinates.PolygonPoints, original), Is.Empty);
            IReadOnlyList<SceneZoneSegmentConflict> conflicts = SceneZoneOverlap.FindPolygonConflicts(overlapping.PolygonPoints, original);
            Assert.That(conflicts, Is.Not.Empty);
            Assert.That(conflicts.Any(value => value.CandidateSegmentIndex >= 0 && value.ExistingSegmentIndex >= 0), Is.True, "Diagnostics identify exact candidate/existing segment pairs.");
            Assert.That(adjacentWithSharedCoordinates.PolygonPoints[1], Is.EqualTo(original.PolygonPoints[1]), "Reused vertices preserve the exact stored coordinate.");
            Assert.That(adjacentWithSharedCoordinates.PolygonPoints[2], Is.EqualTo(original.PolygonPoints[2]));
        }

        [Test]
        public void StandaloneCenterAndCircleOverlap_RespectAuthoredCenterAndTangency()
        {
            SceneZoneAsset polygon = ScriptableObject.CreateInstance<SceneZoneAsset>();
            SceneZoneAsset tangentCircle = ScriptableObject.CreateInstance<SceneZoneAsset>();
            SceneZoneAsset overlappingCircle = ScriptableObject.CreateInstance<SceneZoneAsset>();
            transients.Add(polygon);
            transients.Add(tangentCircle);
            transients.Add(overlappingCircle);
            Vector3 authoredCenter = new Vector3(5f, 2f, 5f);
            polygon.Configure("test.centered", "Centered", SceneZoneShape.Polygon, Square(0f, 10f), useAuthoredCenter: true, authoredCenter: authoredCenter);
            tangentCircle.Configure("test.tangent", "Tangent", SceneZoneShape.Circle, center: new Vector3(20f, 0f, 5f), radius: 10f, useAuthoredCenter: true, authoredCenter: new Vector3(20f, 0f, 5f));
            overlappingCircle.Configure("test.circle-overlap", "Overlap", SceneZoneShape.Circle, center: new Vector3(19f, 0f, 5f), radius: 10f);

            Assert.That(polygon.HasCenterPoint, Is.True);
            Assert.That(polygon.CenterPoint2D, Is.EqualTo(new Vector2(authoredCenter.x, authoredCenter.z)));
            Assert.That(polygon.CenterPoint, Is.EqualTo(new Vector3(authoredCenter.x, 0f, authoredCenter.z)));
            Assert.That(polygon.Validate(out string failure), Is.True, failure);
            Assert.That(SceneZoneOverlap.CircleOverlaps(tangentCircle.CircleCenter, tangentCircle.CircleRadius, polygon), Is.False, "Tangency is a valid shared border.");
            Assert.That(SceneZoneOverlap.CircleOverlaps(overlappingCircle.CircleCenter, overlappingCircle.CircleRadius, polygon), Is.True);
            Assert.That(SceneZoneOverlap.ContainsHorizontal(polygon, authoredCenter), Is.True);
        }

        [Test]
        public void StandalonePolygonValidation_RejectsSelfIntersectionAndDuplicateEdges()
        {
            SceneZoneAsset crossing = Geometry("test.crossing", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 4f),
                new Vector3(0f, 0f, 4f), new Vector3(3f, 0f, 0f)
            });
            SceneZoneAsset duplicate = Geometry("test.duplicate", new[]
            {
                Vector3.zero, Vector3.right * 4f, Vector3.right * 4f, Vector3.forward * 4f
            });

            Assert.That(crossing.ApproximateHorizontalArea(), Is.GreaterThan(0f), "A nonzero shoelace area alone must not qualify a polygon.");
            Assert.That(crossing.Validate(out string crossingFailure), Is.False);
            Assert.That(crossingFailure, Does.Contain("intersects itself"));
            Assert.That(duplicate.Validate(out string duplicateFailure), Is.False);
            Assert.That(duplicateFailure, Does.Contain("duplicate"));
        }

        [Test]
        public void StandaloneCalculatedCenter_RemainsInsideConcavePolygon()
        {
            SceneZoneAsset concave = Geometry("test.concave", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(6f, 0f, 0f),
                new Vector3(6f, 0f, 1f), new Vector3(1f, 0f, 1f),
                new Vector3(1f, 0f, 5f), new Vector3(6f, 0f, 5f),
                new Vector3(6f, 0f, 6f), new Vector3(0f, 0f, 6f)
            });

            Assert.That(concave.Validate(out string failure), Is.True, failure);
            Assert.That(concave.Contains(concave.CalculatedCenter), Is.True);
        }

        [Test]
        public void RuntimeLineRenderer_RefreshesWhenReferencedZoneChanges()
        {
            SceneZoneAsset zone = Geometry("test.renderer", Square(0f, 10f));
            GameObject display = new GameObject("Scene zone line renderer test");
            transients.Add(display);
            SceneZoneLineRenderer renderer = display.AddComponent<SceneZoneLineRenderer>();
            renderer.Configure(zone);
            LineRenderer line = display.GetComponent<LineRenderer>();

            Assert.That(line.positionCount, Is.EqualTo(4));
            Assert.That(line.GetPosition(1).x, Is.EqualTo(10f));

            zone.Configure("test.renderer", "test.renderer", SceneZoneShape.Polygon, Square(0f, 20f));

            Assert.That(line.positionCount, Is.EqualTo(4));
            Assert.That(line.GetPosition(1).x, Is.EqualTo(20f));
        }

        [Test]
        public void Runtime_UsesSpecificPriorityAndFallsBackThroughExclusionHole()
        {
            PlaceDefinition town = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeTownPlace.asset");
            PlaceDefinition outskirts = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeOutskirtsPlace.asset");
            Assert.That(town, Is.Not.Null);
            Assert.That(outskirts, Is.Not.Null);

            SceneZoneAsset outer = Geometry("test.outer", Square(-100f, 100f));
            SceneZoneAsset townShape = Geometry("test.town", Square(-20f, 20f));
            SceneZoneAsset townHole = Geometry("test.hole", Square(-2f, 2f));
            SpatialTerritoryBoundaryDefinition outerBinding = Binding("spatial-boundary.test.outer", outer, outskirts, "location.prototype.wilderness-ring", SpatialBoundaryOperation.Include, 0);
            SpatialTerritoryBoundaryDefinition townBinding = Binding("spatial-boundary.test.town", townShape, town, "location.prototype.town", SpatialBoundaryOperation.Include, 100);
            SpatialTerritoryBoundaryDefinition holeBinding = Binding("spatial-boundary.test.hole", townHole, town, "location.prototype.town", SpatialBoundaryOperation.Exclude, 100);
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { town, outskirts, outerBinding, townBinding, holeBinding });
            SpatialTerritoryBoundaryRuntime runtime = new SpatialTerritoryBoundaryRuntime();
            runtime.Configure(registry, null);

            Assert.That(runtime.Resolve(new Vector3(10f, 0f, 10f), "scene.test").PrimaryPlaceId, Is.EqualTo(town.Id));
            Assert.That(runtime.Resolve(Vector3.zero, "scene.test").PrimaryPlaceId, Is.EqualTo(outskirts.Id), "The exclusion hole removes only the town candidate.");
            Assert.That(runtime.Resolve(new Vector3(50f, 0f, 50f), "scene.test").PrimaryPlaceId, Is.EqualTo(outskirts.Id));
            Assert.That(runtime.Resolve(new Vector3(200f, 0f, 200f), "scene.test").IsResolved, Is.False);
            Assert.That(runtime.Resolve(new Vector3(10f, 0f, 10f), "scene.other").IsResolved, Is.False, "Runtime scene scope belongs to the game-specific binding, not generic geometry.");
        }

        [Test]
        public void Tracker_FeedsExistingCurrentPlaceTrackerWithoutSceneVolumes()
        {
            PlaceDefinition town = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeTownPlace.asset");
            PlaceDefinition outskirts = AssetDatabase.LoadAssetAtPath<PlaceDefinition>("Assets/_Project/Content/Places/PrototypeOutskirtsPlace.asset");
            SceneZoneAsset outer = Geometry("test.tracker.outer", Square(-100f, 100f));
            SceneZoneAsset inner = Geometry("test.tracker.inner", Square(-10f, 10f));
            SpatialTerritoryBoundaryDefinition outerBinding = Binding("spatial-boundary.test.tracker.outer", outer, outskirts, "location.prototype.wilderness-ring", SpatialBoundaryOperation.Include, 0);
            SpatialTerritoryBoundaryDefinition innerBinding = Binding("spatial-boundary.test.tracker.inner", inner, town, "location.prototype.town", SpatialBoundaryOperation.Include, 100);
            SpatialTerritoryBoundaryRuntime runtime = new SpatialTerritoryBoundaryRuntime();
            runtime.Configure(new DefinitionRegistry(new IGameDefinition[] { town, outskirts, outerBinding, innerBinding }), null);

            GameObject player = new GameObject("Spatial tracker test");
            transients.Add(player);
            CurrentPlaceTracker currentPlace = player.AddComponent<CurrentPlaceTracker>();
            SpatialTerritoryBoundaryTracker tracker = player.AddComponent<SpatialTerritoryBoundaryTracker>();
            player.transform.position = Vector3.zero;
            tracker.Configure(runtime, currentPlace, "scene.test");
            Assert.That(currentPlace.CurrentPlaceId, Is.EqualTo(town.Id));

            player.transform.position = new Vector3(50f, 0f, 50f);
            tracker.SampleNow();
            Assert.That(currentPlace.CurrentPlaceId, Is.EqualTo(outskirts.Id));
        }

        [Test]
        public void PrototypeCatalog_ResolvesAuthoredTownAndOutskirtsBoundaries()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();
            Assert.That(registry.DefinitionsById.TryGetValue("spatial-boundary.prototype.town", out IGameDefinition townDefinition), Is.True);
            Assert.That(registry.DefinitionsById.TryGetValue("spatial-boundary.prototype.outskirts", out IGameDefinition outskirtsDefinition), Is.True);
            SpatialTerritoryBoundaryDefinition townBinding = townDefinition as SpatialTerritoryBoundaryDefinition;
            SpatialTerritoryBoundaryDefinition outskirtsBinding = outskirtsDefinition as SpatialTerritoryBoundaryDefinition;
            SceneZoneLayerAsset settlements = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(SettlementLayerPath);
            SceneZoneLayerAsset regional = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(RegionalLayerPath);
            Assert.That(townBinding, Is.Not.Null);
            Assert.That(outskirtsBinding, Is.Not.Null);
            Assert.That(townBinding.ZoneBoundary, Is.SameAs(settlements.GetZone("zone.prototype-town")));
            Assert.That(outskirtsBinding.ZoneBoundary, Is.SameAs(regional.GetZone("zone.prototype-outskirts")));

            SpatialTerritoryBoundaryRuntime runtime = new SpatialTerritoryBoundaryRuntime();
            runtime.Configure(registry, null);

            SpatialTerritoryResolution playerStart = runtime.Resolve(new Vector3(-29.2f, 0f, 69f), "scene.prototype");
            SpatialTerritoryResolution surroundingLand = runtime.Resolve(new Vector3(200f, 0f, 200f), "scene.prototype");
            SpatialTerritoryResolution outsideAuthoredMap = runtime.Resolve(new Vector3(700f, 0f, 700f), "scene.prototype");

            Assert.That(playerStart.PrimaryPlaceId, Is.EqualTo("place.settlement.prototype-town"));
            Assert.That(playerStart.LocationId, Is.EqualTo("location.prototype.town"));
            Assert.That(surroundingLand.PrimaryPlaceId, Is.EqualTo("place.wilderness.prototype-outskirts"));
            Assert.That(surroundingLand.LocationId, Is.EqualTo("location.prototype.wilderness-ring"));
            Assert.That(outsideAuthoredMap.IsResolved, Is.False);
        }

        [Test]
        public void PrototypeLayers_KeepEveryZoneInsideItsSingleLayerFile()
        {
            SceneZoneLayerAsset regional = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(RegionalLayerPath);
            SceneZoneLayerAsset settlements = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(SettlementLayerPath);
            Assert.That(regional, Is.Not.Null);
            Assert.That(settlements, Is.Not.Null);
            Assert.That(regional.Zones.Count, Is.EqualTo(1));
            Assert.That(settlements.Zones.Count, Is.EqualTo(1));
            Assert.That(regional.GetZone("zone.prototype-outskirts"), Is.Not.Null);
            Assert.That(settlements.FindZoneByName("Prototype Town Boundary"), Is.Not.Null);
            Assert.That(regional.GetZonePoints("zone.prototype-outskirts").Count, Is.EqualTo(4));
            Assert.That(regional.ScenePath, Is.EqualTo("Assets/_Project/Scenes/Prototype/PrototypeScene.unity"));
            Assert.That(settlements.ScenePath, Is.EqualTo("Assets/_Project/Scenes/Prototype/PrototypeScene.unity"));
            string prototypeSceneGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scenes/Prototype/PrototypeScene.unity");
            Assert.That(prototypeSceneGuid, Is.Not.Empty);
            Assert.That(regional.SceneGuid, Is.EqualTo(prototypeSceneGuid));
            Assert.That(settlements.SceneGuid, Is.EqualTo(prototypeSceneGuid));
            Assert.That(regional.Validate(out string regionalFailure), Is.True, regionalFailure);
            Assert.That(settlements.Validate(out string settlementFailure), Is.True, settlementFailure);
            Assert.That(AssetDatabase.GetAssetPath(regional.Zones[0]), Is.EqualTo(RegionalLayerPath));
            Assert.That(AssetDatabase.GetAssetPath(settlements.Zones[0]), Is.EqualTo(SettlementLayerPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneZoneAsset>("Assets/_Project/Content/World/SpatialBoundaries/Geometry/PrototypeOutskirtsBoundary.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneZoneAsset>("Assets/_Project/Content/World/SpatialBoundaries/Geometry/PrototypeTownBoundary.asset"), Is.Null);
        }

        private SceneZoneAsset Geometry(string id, Vector3[] points, SceneZoneLayerAsset layer = null)
        {
            SceneZoneAsset asset = ScriptableObject.CreateInstance<SceneZoneAsset>();
            transients.Add(asset);
            asset.Configure(id, id, SceneZoneShape.Polygon, points, authoredLayer: layer);
            return asset;
        }

        private SpatialTerritoryBoundaryDefinition Binding(string id, SceneZoneAsset geometry, PlaceDefinition place, string locationId, SpatialBoundaryOperation operation, int priority)
        {
            SpatialTerritoryBoundaryDefinition asset = ScriptableObject.CreateInstance<SpatialTerritoryBoundaryDefinition>();
            transients.Add(asset);
            asset.DevelopmentConfigure(id, id, geometry, place, locationId, "scene.test", operation, priority);
            return asset;
        }

        private static Vector3[] Square(float minimum, float maximum) => new[]
        {
            new Vector3(minimum, 0f, minimum), new Vector3(maximum, 0f, minimum),
            new Vector3(maximum, 0f, maximum), new Vector3(minimum, 0f, maximum)
        };
    }
}
