#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class WorldLocationIdentityFoundationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeLocationDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeLocationDefinitionFactory.SettlementDefinitionId, out LocationDefinition settlement), Is.True);
            Assert.That(settlement.Category, Is.EqualTo(LocationCategory.Settlement));
            Assert.That(settlement.ClassificationDomain, Is.EqualTo(CategoryDomain.Place));
            Assert.That(settlement.SupportsVisibility(LocationVisibility.Public), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (LocationDefinition definition in PrototypeLocationDefinitionFactory.CreateMissingLocationDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That((int)CategoryDomain.Place, Is.EqualTo(6), "Location definitions must reuse Place domain instead of renumbering serialized category domains.");
            Assert.That(Enum.IsDefined(typeof(InformationSubjectType), InformationSubjectType.Location), Is.True);
        }

        [Test]
        public void RuntimeLocationsUseStableIdentitySeparateFromDefinitionsAndScenes()
        {
            LocationRuntime runtime = CreateRuntime();
            LocationOperationResult preview = runtime.CreateLocation(CreateRequest("location.test.guild", "Prototype Guild Hall", preview: true, sceneBinding: "old.scene.key"));
            LocationOperationResult create = runtime.CreateLocation(CreateRequest("location.test.guild", "Prototype Guild Hall", sceneBinding: "old.scene.key"));
            LocationSnapshot beforeRename = create.Snapshot;
            LocationRecordData mutatedSnapshot = beforeRename.ToSaveData();
            mutatedSnapshot.officialName = "Mutated Snapshot";
            LocationOperationResult rename = runtime.RenameLocation(new LocationRenameRequest
            {
                transactionId = "tx.location.rename.guild",
                locationId = "location.test.guild",
                newName = "Renamed Guild Hall",
                category = LocationNameCategory.Official,
                effectiveWorldTime = 10d
            });

            Assert.That(preview.Status, Is.EqualTo(LocationOperationStatus.Preview));
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(rename.Succeeded, Is.True, rename.Message);
            Assert.That(rename.Snapshot.LocationId, Is.EqualTo("location.test.guild"));
            Assert.That(rename.Snapshot.LocationDefinitionId, Is.EqualTo(PrototypeLocationDefinitionFactory.GuildHallDefinitionId));
            Assert.That(rename.Snapshot.PrototypeSceneBindingKey, Is.EqualTo("old.scene.key"));
            Assert.That(beforeRename.OfficialName, Is.EqualTo("Prototype Guild Hall"));
        }

        [Test]
        public void AssociationsRemainReferencesAndDoNotOwnExternalRecords()
        {
            LocationRuntime runtime = CreateRuntime();
            LocationOperationResult create = runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = "tx.location.associated",
                locationId = "location.test.civic",
                locationDefinitionId = PrototypeLocationDefinitionFactory.GovernmentBuildingDefinitionId,
                officialName = "Civic Office",
                associatedPropertyId = "property.prototype.guild-building",
                associatedOrganizationId = "organization.prototype.government",
                associatedGovernmentId = "government.prototype.civic",
                associatedTerritoryIds = new[] { "territory.prototype.village" },
                semanticTagIds = new[] { "government", "building", "civic" },
                associations = new[] { new LocationAssociationReferenceData { kind = LocationAssociationKind.Provenance, referenceId = "record.prototype.charter", worldId = PersistenceService.LocalWorldId } }
            });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(create.Snapshot.AssociatedPropertyId, Is.EqualTo("property.prototype.guild-building"));
            Assert.That(create.Snapshot.AssociatedOrganizationId, Is.EqualTo("organization.prototype.government"));
            Assert.That(create.Snapshot.AssociatedGovernmentId, Is.EqualTo("government.prototype.civic"));
            Assert.That(create.Snapshot.AssociatedTerritoryIds, Does.Contain("territory.prototype.village"));
            Assert.That(create.Snapshot.Associations.Single().kind, Is.EqualTo(LocationAssociationKind.Provenance));
            Assert.That(runtime.ValidateRuntime().Succeeded, Is.True, runtime.ValidateRuntime().Summary);
        }

        [Test]
        public void LifecycleReferencesQueriesAndRevisionSafetyAreDeterministic()
        {
            LocationRuntime runtime = CreateRuntime();
            LocationOperationResult create = runtime.CreateLocation(CreateRequest("location.test.wilderness", "Northern Ring", definitionId: PrototypeLocationDefinitionFactory.WildernessDefinitionId, tags: new[] { "wilderness", "outdoor" }));
            LocationOperationResult duplicate = runtime.CreateLocation(CreateRequest("location.test.wilderness", "Northern Ring", definitionId: PrototypeLocationDefinitionFactory.WildernessDefinitionId, tags: new[] { "wilderness", "outdoor" }));
            LocationOperationResult stale = runtime.RenameLocation(new LocationRenameRequest { transactionId = "tx.location.stale", locationId = "location.test.wilderness", newName = "Stale", expectedRevision = 0L });
            LocationOperationResult historical = runtime.TransitionLifecycle(new LocationLifecycleTransitionRequest { transactionId = "tx.location.historical", locationId = "location.test.wilderness", targetState = LocationLifecycleState.Historical, worldTime = 50d });
            LocationReferenceResolutionResult reference = runtime.ResolveReference(historical.Snapshot.ToReference());

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(stale.Status, Is.EqualTo(LocationOperationStatus.RevisionConflict));
            Assert.That(historical.Succeeded, Is.True, historical.Message);
            Assert.That(reference.Succeeded, Is.True, reference.Message);
            Assert.That(runtime.QueryByTag("wilderness").Select(item => item.LocationId), Is.EqualTo(runtime.QueryByTag("wilderness").Select(item => item.LocationId).OrderBy(id => id, StringComparer.Ordinal)));
            Assert.That(runtime.QueryByCategory(LocationCategory.Wilderness).Any(item => item.LocationId == "location.test.wilderness"), Is.True);
        }

        [Test]
        public void PersistenceParticipantRejectsInvalidRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            LocationRuntime runtime = CreateRuntime(registry);
            LocationOperationResult create = runtime.CreateLocation(CreateRequest("location.test.persisted", "Persisted Guild Hall"));
            LocationPersistenceParticipant participant = new LocationPersistenceParticipant(runtime, () => registry, PersistenceService.LocalWorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            LocationRuntimeSaveData corrupt = JsonUtility.FromJson<LocationRuntimeSaveData>(save.PayloadJson);
            corrupt.records[0].locationDefinitionId = "location-definition.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.TryGetSnapshot("location.test.persisted", out LocationSnapshot snapshot), Is.True);
            Assert.That(snapshot.OfficialName, Is.EqualTo("Persisted Guild Hall"));
        }

        [Test]
        public void SaveRestorePreservesSubjectReferencesAndSceneIndependentIds()
        {
            DefinitionRegistry registry = CreateRegistry();
            LocationRuntime runtime = CreateRuntime(registry);
            runtime.CreateLocation(CreateRequest("location.test.secret-dungeon", "Secret Dungeon", definitionId: PrototypeLocationDefinitionFactory.DungeonDefinitionId, visibility: LocationVisibility.Hidden, sceneBinding: "scene.marker.secret-dungeon", tags: new[] { "dungeon", "hazard", "interior" }));
            LocationRuntimeSaveData save = runtime.CreateSaveData();
            LocationRuntime restored = CreateRuntime(registry);
            restored.Reset();

            LocationOperationResult restore = restored.RestoreFromSaveData(save, registry, PersistenceService.LocalWorldId);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetSnapshot("location.test.secret-dungeon", out LocationSnapshot snapshot), Is.True);
            Assert.That(snapshot.LocationId, Is.EqualTo("location.test.secret-dungeon"));
            Assert.That(snapshot.PrototypeSceneBindingKey, Is.EqualTo("scene.marker.secret-dungeon"));
            Assert.That(snapshot.ToInformationSubject().subjectType, Is.EqualTo(InformationSubjectType.Location));
            Assert.That(snapshot.ToInformationSubject().subjectId, Is.EqualTo("location.test.secret-dungeon"));
        }

        private static LocationCreateRequest CreateRequest(string locationId, string name, string definitionId = PrototypeLocationDefinitionFactory.GuildHallDefinitionId, bool preview = false, LocationVisibility visibility = LocationVisibility.Public, string sceneBinding = null, string[] tags = null)
        {
            return new LocationCreateRequest
            {
                transactionId = $"tx.location.create.{locationId}",
                locationId = locationId,
                locationDefinitionId = definitionId,
                officialName = name,
                commonName = name,
                initialLifecycleState = LocationLifecycleState.Active,
                semanticTagIds = tags ?? new[] { "guild", "building", "service" },
                associatedOrganizationId = definitionId == PrototypeLocationDefinitionFactory.GuildHallDefinitionId ? "organization.prototype.adventurers-guild" : string.Empty,
                prototypeSceneBindingKey = sceneBinding,
                visibility = visibility,
                sourceEventId = "event.location.test",
                provenanceId = "test.location",
                preview = preview
            };
        }

        private static LocationRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            LocationRuntime runtime = new LocationRuntime();
            runtime.Configure(registry ?? CreateRegistry(), PersistenceService.LocalWorldId);
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
