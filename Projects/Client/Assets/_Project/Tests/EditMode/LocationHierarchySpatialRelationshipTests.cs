#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class LocationHierarchySpatialRelationshipTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeSeedCreatesValidAuthoritativeContainmentGraph()
        {
            LocationRuntime runtime = CreateSeededRuntime();

            LocationContainmentSnapshot parent = runtime.GetActiveParentLink("location.prototype.guildmaster-office");
            LocationHierarchyPathResult path = runtime.GetHierarchyPath("location.prototype.guildmaster-office");

            Assert.That(runtime.ValidateRuntime().Succeeded, Is.True, runtime.ValidateRuntime().Summary);
            Assert.That(parent, Is.Not.Null);
            Assert.That(parent.ParentLocationId, Is.EqualTo("location.prototype.adventurers-guild"));
            Assert.That(path.Succeeded, Is.True, path.Message);
            Assert.That(path.Path.Select(item => item.LocationId), Is.EqualTo(new[]
            {
                "location.prototype.world",
                "location.prototype.region",
                "location.prototype.town",
                "location.prototype.adventurers-guild",
                "location.prototype.guildmaster-office"
            }));
            string[] descendantIds = runtime.GetDescendants("location.prototype.town").Select(item => item.LocationId).ToArray();
            Assert.That(descendantIds, Is.EqualTo(descendantIds.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        [Test]
        public void AssignContainmentRejectsSecondParentAndCyclesWithoutMutation()
        {
            LocationRuntime runtime = CreateSeededRuntime();
            LocationOperationResult parent = Create(runtime, "location.test.cycle-parent", "Cycle Parent", PrototypeLocationDefinitionFactory.RoomDefinitionId, "room", "interior");
            LocationOperationResult child = Create(runtime, "location.test.cycle-child", "Cycle Child", PrototypeLocationDefinitionFactory.RoomDefinitionId, "room", "interior");
            LocationOperationResult initial = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = "tx.location.containment.initial",
                linkId = "location-containment.test.initial",
                parentLocationId = parent.Snapshot.LocationId,
                childLocationId = child.Snapshot.LocationId,
                kind = LocationContainmentKind.Interior
            });
            long beforeCycle = runtime.Revision;

            LocationOperationResult cycle = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = "tx.location.containment.cycle",
                linkId = "location-containment.test.cycle",
                parentLocationId = child.Snapshot.LocationId,
                childLocationId = parent.Snapshot.LocationId,
                kind = LocationContainmentKind.Interior
            });
            LocationOperationResult secondParent = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = "tx.location.containment.second-parent",
                linkId = "location-containment.test.second-parent",
                parentLocationId = "location.prototype.market-district",
                childLocationId = "location.prototype.adventurers-guild"
            });

            Assert.That(initial.Succeeded, Is.True, initial.Message);
            Assert.That(cycle.Status, Is.EqualTo(LocationOperationStatus.CycleDetected), cycle.Message);
            Assert.That(runtime.Revision, Is.EqualTo(beforeCycle));
            Assert.That(secondParent.Status, Is.EqualTo(LocationOperationStatus.ActiveParentConflict), secondParent.Message);
            Assert.That(runtime.GetActiveParentLink("location.prototype.adventurers-guild").ParentLocationId, Is.EqualTo("location.prototype.town"));
        }

        [Test]
        public void ReparentPreservesHistoricalLinksAndStableChildIdentity()
        {
            LocationRuntime runtime = CreateSeededRuntime();
            LocationOperationResult room = Create(runtime, "location.test.reparent", "Reparent Room", PrototypeLocationDefinitionFactory.RoomDefinitionId, "room", "interior");
            LocationOperationResult first = runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = "tx.location.containment.first",
                linkId = "location-containment.test.first",
                parentLocationId = "location.prototype.adventurers-guild",
                childLocationId = room.Snapshot.LocationId,
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 1d
            });
            LocationOperationResult reparent = runtime.ReparentLocation(new LocationReparentRequest
            {
                transactionId = "tx.location.containment.reparent",
                oldParentLocationId = "location.prototype.adventurers-guild",
                newParentLocationId = "location.prototype.civic-office",
                childLocationId = room.Snapshot.LocationId,
                newLinkId = "location-containment.test.reparented",
                kind = LocationContainmentKind.Interior,
                effectiveWorldTime = 2d
            });

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(reparent.Succeeded, Is.True, reparent.Message);
            Assert.That(runtime.GetActiveParentLink(room.Snapshot.LocationId).ParentLocationId, Is.EqualTo("location.prototype.civic-office"));
            Assert.That(runtime.ContainmentLinks.Any(link => link.LinkId == "location-containment.test.first" && link.State == LocationLinkState.Ended), Is.True);
            Assert.That(runtime.TryGetSnapshot(room.Snapshot.LocationId, out LocationSnapshot after), Is.True);
            Assert.That(after.LocationDefinitionId, Is.EqualTo(room.Snapshot.LocationDefinitionId));
        }

        [Test]
        public void SpatialRelationshipsResolveSymmetricAndInverseQueriesWithoutRouteState()
        {
            LocationRuntime runtime = CreateSeededRuntime();
            LocationOperationResult above = runtime.CreateSpatialRelationship(new LocationSpatialRelationshipRequest
            {
                transactionId = "tx.location.spatial.above",
                relationshipId = "location-spatial.test.above",
                sourceLocationId = "location.prototype.guildmaster-office",
                targetLocationId = "location.prototype.basement-prison",
                kind = LocationSpatialRelationshipKind.Above,
                directionality = LocationSpatialDirectionality.Directional
            });

            Assert.That(above.Succeeded, Is.True, above.Message);
            Assert.That(runtime.AreSpatiallyRelated("location.prototype.guildmaster-office", "location.prototype.basement-prison", LocationSpatialRelationshipKind.Above), Is.True);
            Assert.That(runtime.AreSpatiallyRelated("location.prototype.basement-prison", "location.prototype.guildmaster-office", LocationSpatialRelationshipKind.Below), Is.True);
            Assert.That(runtime.AreSpatiallyRelated("location.prototype.adventurers-guild", "location.prototype.market-district", LocationSpatialRelationshipKind.Near), Is.True);
            Assert.That(runtime.GetSpatialRelationships("location.prototype.guildmaster-office").First().ToSaveData().GetType().GetField("routeCost"), Is.Null);
        }

        [Test]
        public void SaveRestorePreservesGraphAndRejectsCorruptCycleBeforeMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            LocationRuntime runtime = CreateSeededRuntime(registry);
            LocationRuntimeSaveData save = runtime.CreateSaveData();
            LocationRuntime restored = CreateRuntime(registry);

            LocationOperationResult restore = restored.RestoreFromSaveData(save, registry, PersistenceService.LocalWorldId);
            LocationRuntimeSaveData corrupt = save.Clone();
            corrupt.containmentLinks.Add(new LocationContainmentLinkData
            {
                linkId = "location-containment.test.corrupt-cycle",
                parentLocationId = "location.prototype.guildmaster-office",
                childLocationId = "location.prototype.town",
                kind = LocationContainmentKind.Primary,
                state = LocationLinkState.Active
            });
            long before = restored.Revision;
            LocationOperationResult rejected = restored.RestoreFromSaveData(corrupt, registry, PersistenceService.LocalWorldId);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.GetActiveParentLink("location.prototype.guildmaster-office").ParentLocationId, Is.EqualTo("location.prototype.adventurers-guild"));
            Assert.That(rejected.Status, Is.EqualTo(LocationOperationStatus.PersistenceInvalid), rejected.Message);
            Assert.That(restored.Revision, Is.EqualTo(before));
            Assert.That(restored.GetActiveParentLink("location.prototype.town").ParentLocationId, Is.EqualTo("location.prototype.region"));
        }

        [Test]
        public void SnapshotsAreImmutableAndVisibilityProjectionCanOmitSecretLinks()
        {
            LocationRuntime runtime = CreateSeededRuntime();
            LocationContainmentSnapshot link = runtime.GetActiveParentLink("location.prototype.dungeon-entry");
            LocationContainmentLinkData mutated = link.ToSaveData();
            mutated.parentLocationId = "location.prototype.world";

            Assert.That(runtime.GetActiveParentLink("location.prototype.dungeon-entry").ParentLocationId, Is.EqualTo("location.prototype.wilderness-ring"));
            Assert.That(runtime.GetChildren("location.prototype.wilderness-ring").Any(item => item.LocationId == "location.prototype.dungeon-entry"), Is.False);
            Assert.That(runtime.GetChildren("location.prototype.wilderness-ring", includeHidden: true).Any(item => item.LocationId == "location.prototype.dungeon-entry"), Is.True);
        }

        private static LocationOperationResult Create(LocationRuntime runtime, string locationId, string name, string definitionId, params string[] tags)
        {
            return runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = $"tx.{locationId}",
                locationId = locationId,
                locationDefinitionId = definitionId,
                officialName = name,
                commonName = name,
                initialLifecycleState = LocationLifecycleState.Active,
                semanticTagIds = tags ?? Array.Empty<string>(),
                sourceEventId = "event.location.hierarchy.test",
                provenanceId = "test.location.hierarchy"
            });
        }

        private static LocationRuntime CreateSeededRuntime(DefinitionRegistry registry = null)
        {
            registry ??= CreateRegistry();
            LocationRuntime runtime = CreateRuntime(registry);
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(runtime, registry, PersistenceService.LocalWorldId);
            return runtime;
        }

        private static LocationRuntime CreateRuntime(DefinitionRegistry registry)
        {
            LocationRuntime runtime = new LocationRuntime();
            runtime.Configure(registry, PersistenceService.LocalWorldId);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(catalog.CreateRegistry());
        }
    }
}
#endif
