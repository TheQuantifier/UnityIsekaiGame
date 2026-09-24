using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class ItemIdentityInstanceStateTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string SwordId = "item.prototype-sword";

        [Test]
        public void TwoItemsSharingDefinitionRemainDistinctAndStableThroughMutation()
        {
            ItemDefinition sword = LoadItem(SwordId, out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();

            ItemInstanceOperationResult first = runtime.CreateItem(sword, ownerPersonId: "person.owner.a", custodianPersonId: "person.borrower");
            ItemInstanceOperationResult second = runtime.CreateItem(sword, ownerPersonId: "person.owner.b", custodianPersonId: "person.owner.b");

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.Snapshot.ItemInstanceId, Is.Not.EqualTo(second.Snapshot.ItemInstanceId));
            Assert.That(first.Snapshot.ItemDefinitionId, Is.EqualTo(SwordId));

            string originalId = first.Snapshot.ItemInstanceId;
            ItemInstanceOperationResult rename = runtime.Rename(originalId, "Borrowed Trial Sword");

            Assert.That(rename.Succeeded, Is.True, rename.Message);
            Assert.That(rename.Snapshot.ItemInstanceId, Is.EqualTo(originalId), "Mutable labels must not replace stable item identity.");
            Assert.That(runtime.QueryByDefinition(SwordId).Count, Is.EqualTo(2));
        }

        [Test]
        public void OwnershipAndCustodyChangeIndependently()
        {
            ItemDefinition sword = LoadItem(SwordId, out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string id = runtime.CreateItem(sword, ownerPersonId: "person.owner", custodianPersonId: "person.borrower").Snapshot.ItemInstanceId;

            ItemInstanceOperationResult custody = runtime.TransferCustody(id, custodianPersonId: "person.carrier");
            ItemInstanceOperationResult ownership = runtime.TransferOwnership(id, ItemOwnershipKind.PersonOwned, ownerPersonId: "person.new-owner");

            Assert.That(custody.Succeeded, Is.True, custody.Message);
            Assert.That(custody.Snapshot.OwnerPersonId, Is.EqualTo("person.owner"));
            Assert.That(custody.Snapshot.CustodianPersonId, Is.EqualTo("person.carrier"));
            Assert.That(ownership.Succeeded, Is.True, ownership.Message);
            Assert.That(ownership.Snapshot.OwnerPersonId, Is.EqualTo("person.new-owner"));
            Assert.That(ownership.Snapshot.CustodianPersonId, Is.EqualTo("person.carrier"), "Ownership transfer must not silently move physical custody.");
        }

        [Test]
        public void LocationStateRejectsDuplicateWorldPlacementAndDestroyedContainment()
        {
            ItemDefinition sword = LoadItem(SwordId, out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string first = runtime.CreateItem(sword).Snapshot.ItemInstanceId;
            string second = runtime.CreateItem(sword).Snapshot.ItemInstanceId;

            Assert.That(runtime.SetWorldPlacement(first, "placement.prototype.sword", "world-entity.sword.a", "scene.prototype").Succeeded, Is.True);
            ItemInstanceOperationResult duplicatePlacement = runtime.SetWorldPlacement(second, "placement.prototype.sword", "world-entity.sword.b", "scene.prototype");
            Assert.That(duplicatePlacement.Succeeded, Is.False);
            Assert.That(duplicatePlacement.Status, Is.EqualTo(ItemInstanceOperationStatus.InvalidLocation));

            ItemInstanceOperationResult destroyed = runtime.DestroyOrConsume(first, consumed: false);
            Assert.That(destroyed.Succeeded, Is.True, destroyed.Message);
            Assert.That(destroyed.Snapshot.LifecycleState, Is.EqualTo(ItemLifecycleState.Destroyed));
            Assert.That(destroyed.Snapshot.LocationKind, Is.EqualTo(ItemLocationKind.Destroyed));

            ItemInstanceOperationResult moveDestroyed = runtime.SetContainerLocation(first, "container.prototype");
            Assert.That(moveDestroyed.Succeeded, Is.False);
            Assert.That(moveDestroyed.Status, Is.EqualTo(ItemInstanceOperationStatus.InvalidState));
        }

        [Test]
        public void SaveRestorePreservesStateAndRejectsDuplicateIdsBeforeMutation()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string id = runtime.CreateItem(sword, creatorPersonId: "person.smith", ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            Assert.That(runtime.AssignMakerMarkAndSerial(id, "smith.mark", "S-0001").Succeeded, Is.True);
            Assert.That(runtime.SetInventoryLocation(id, "person.owner").Succeeded, Is.True);

            ItemInstanceRuntimeSaveData saveData = runtime.CreateSaveData();
            ItemInstanceIdentityRuntime restored = new ItemInstanceIdentityRuntime();
            ItemInstanceOperationResult restore = restored.RestoreFromSaveData(saveData, registry);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetSnapshot(id, out ItemInstanceSnapshot snapshot), Is.True);
            Assert.That(snapshot.MakerMark, Is.EqualTo("smith.mark"));
            Assert.That(snapshot.SerialNumber, Is.EqualTo("S-0001"));
            Assert.That(snapshot.LocationKind, Is.EqualTo(ItemLocationKind.Inventory));

            ItemInstanceRuntimeSaveData corrupt = saveData.Clone();
            corrupt.records.Add(saveData.records[0].Clone());
            ItemInstanceIdentityRuntime failedRestore = new ItemInstanceIdentityRuntime();
            string existingId = failedRestore.CreateItem(sword).Snapshot.ItemInstanceId;
            ItemInstanceOperationResult rejected = failedRestore.RestoreFromSaveData(corrupt, registry);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(failedRestore.TryGetSnapshot(existingId, out _), Is.True, "Failed prepare must leave live state unchanged.");
        }

        [Test]
        public void SnapshotsAndAccessSubjectsAreImmutableCopies()
        {
            ItemDefinition sword = LoadItem(SwordId, out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string id = runtime.CreateItem(sword, ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            ItemInstanceSnapshot before = runtime.Rename(id, "Old Name").Snapshot;

            before.Data.labels.customName = "Tampered Name";
            before.Data.tags = new[] { "tampered" };
            Assert.That(runtime.Rename(id, "New Name").Succeeded, Is.True);
            Assert.That(runtime.TryGetSnapshot(id, out ItemInstanceSnapshot after), Is.True);

            Assert.That(after.CustomName, Is.EqualTo("New Name"));
            Assert.That(after.Tags, Does.Not.Contain("tampered"));

            InformationSubjectReferenceData subject = after.CreateInformationSubject();
            Assert.That(subject.subjectType, Is.EqualTo(InformationSubjectType.Custom));
            Assert.That(subject.subjectId, Is.EqualTo(id));
            Assert.That(subject.parentSubjectId, Is.EqualTo(SwordId));
            Assert.That(subject.tags, Does.Contain("item.instance"));
            Assert.That(subject.tags, Does.Contain(ItemInformationSubject.ItemInstanceSubjectTag));
            Assert.That(ItemInformationSubject.ProtectedFields, Does.Contain("serial"));
            Assert.That(ItemInformationSubject.ProtectedFields, Does.Contain("provenance"));

            ItemInstanceProjection projection = runtime.Project(id, ItemProjectionAudience.PublicInspection);
            Assert.That(projection.Denied, Is.False);
            Assert.That(projection.Snapshot.ItemInstanceId, Is.EqualTo(id));
        }

        [Test]
        public void InventoryEquipmentProjectionBuildsOneCoherentIdentityGraph()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            ItemDefinition potion = LoadItem("item.health-potion", out _);
            string inventorySwordId = ItemInstanceId.Generate();
            string equippedSwordId = ItemInstanceId.Generate();
            PlayerInventoryEquipmentSaveData projection = new PlayerInventoryEquipmentSaveData
            {
                inventory = new InventorySaveData
                {
                    slotCapacity = 3,
                    entries =
                    {
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.DefinitionStack, definitionId = potion.ItemId, quantity = 2 },
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = inventorySwordId, quantity = 1 }
                    }
                },
                equipment = new EquipmentSaveData
                {
                    slots =
                    {
                        new EquipmentSlotSaveData { slotType = EquipmentSlotType.MainHand, mode = EquipmentEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = equippedSwordId }
                    }
                }
            };

            ItemIdentityInventoryBridgeResult migration = ItemIdentityInventoryBridge.BuildInventoryEquipmentProjection(projection, registry, "person.prototype.player", "test.projection");

            Assert.That(migration.Succeeded, Is.True, migration.Message);
            Assert.That(migration.SaveData.records.Count, Is.EqualTo(3));
            Assert.That(migration.SaveData.records.Exists(record => record.itemInstanceId == inventorySwordId && record.location.kind == ItemLocationKind.Inventory), Is.True);
            Assert.That(migration.SaveData.records.Exists(record => record.itemInstanceId == equippedSwordId && record.location.kind == ItemLocationKind.Equipped), Is.True);
            ItemInstanceRecordData stack = migration.SaveData.records.Find(record => record.itemDefinitionId == potion.ItemId);
            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.classification, Is.EqualTo(ItemInstanceClassification.Fungible));
            Assert.That(stack.stackQuantity, Is.EqualTo(2));

            ItemIdentityInventoryBridgeResult audit = ItemIdentityInventoryBridge.ValidateInventoryEquipmentProjection(projection, migration.SaveData, "person.prototype.player");
            Assert.That(audit.Succeeded, Is.True, audit.Message);
        }

        [Test]
        public void LiveInventoryEquipmentChangesSynchronizeIntoItemIdentityRuntime()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            GameObject player = new GameObject("Item Identity Synchronizer Test Player");

            try
            {
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = player.AddComponent<PlayerEquipment>();
                ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
                PlayerItemIdentitySynchronizer synchronizer = player.AddComponent<PlayerItemIdentitySynchronizer>();
                synchronizer.Configure(inventory, equipment, runtime, () => registry, "person.prototype.player", "test.live-sync");

                InventoryAddResult add = inventory.AddItemOrInstances(sword, 1);
                Assert.That(add.AddedAll, Is.True);
                Assert.That(runtime.QueryByDefinition(SwordId).Count, Is.EqualTo(1));
                ItemInstanceSnapshot inventorySnapshot = runtime.QueryByDefinition(SwordId)[0];
                Assert.That(inventorySnapshot.LocationKind, Is.EqualTo(ItemLocationKind.Inventory));

                EquipmentOperationResult equip = equipment.EquipFromInventorySlot(0);
                Assert.That(equip.Succeeded, Is.True, equip.Message);
                Assert.That(runtime.TryGetSnapshot(inventorySnapshot.ItemInstanceId, out ItemInstanceSnapshot equippedSnapshot), Is.True);
                Assert.That(equippedSnapshot.LocationKind, Is.EqualTo(ItemLocationKind.Equipped));
                Assert.That(equippedSnapshot.Data.location.equipmentSlotId, Is.EqualTo(EquipmentSlotType.MainHand.ToString()));

                EquipmentOperationResult unequip = equipment.Unequip(EquipmentSlotType.MainHand);
                Assert.That(unequip.Succeeded, Is.True, unequip.Message);
                Assert.That(runtime.TryGetSnapshot(inventorySnapshot.ItemInstanceId, out ItemInstanceSnapshot restoredSnapshot), Is.True);
                Assert.That(restoredSnapshot.LocationKind, Is.EqualTo(ItemLocationKind.Inventory));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CurrentInventoryEquipmentSavesAreIdentityProjectionOnly()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            GameObject player = new GameObject("Current Item Identity Projection Save Test Player");

            try
            {
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = player.AddComponent<PlayerEquipment>();
                ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
                PlayerItemIdentitySynchronizer synchronizer = player.AddComponent<PlayerItemIdentitySynchronizer>();
                synchronizer.Configure(inventory, equipment, runtime, () => registry, "person.prototype.player", "test.current-projection");

                Assert.That(inventory.AddItemOrInstances(sword, 1).AddedAll, Is.True);
                InventorySaveData inventorySave = inventory.CreateSaveData();
                InventoryEntrySaveData inventoryEntry = inventorySave.entries[0];
                Assert.That(inventoryEntry.mode, Is.EqualTo(InventoryEntrySaveMode.StatefulInstance));
                Assert.That(inventoryEntry.definitionId, Is.EqualTo(SwordId));
                Assert.That(ItemInstanceId.IsValid(inventoryEntry.itemInstanceId), Is.True);

                Assert.That(equipment.EquipFromInventorySlot(0).Succeeded, Is.True);
                EquipmentSlotSaveData equippedEntry = equipment.CreateSaveData().slots.Find(slot => slot.slotType == EquipmentSlotType.MainHand);
                Assert.That(equippedEntry, Is.Not.Null);
                Assert.That(equippedEntry.definitionId, Is.EqualTo(SwordId));
                Assert.That(equippedEntry.itemInstanceId, Is.EqualTo(inventoryEntry.itemInstanceId));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PersistenceParticipantRejectsIdentityProjectionDriftBeforeCommit()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            GameObject player = new GameObject("Item Identity Persistence Drift Test Player");

            try
            {
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                PlayerEquipment equipment = player.AddComponent<PlayerEquipment>();
                ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
                PlayerInventoryEquipmentPersistenceParticipant participant = new PlayerInventoryEquipmentPersistenceParticipant(
                    inventory,
                    equipment,
                    () => registry,
                    "person.prototype.player",
                    runtime,
                    "test.persistence-drift");

                Assert.That(inventory.AddItemOrInstances(sword, 1).AddedAll, Is.True);
                PersistenceParticipantSaveResult capture = participant.CapturePayload();
                Assert.That(capture.Succeeded, Is.True, capture.Message);
                string itemInstanceId = runtime.QueryByDefinition(SwordId)[0].ItemInstanceId;
                Assert.That(runtime.SetWorldPlacement(itemInstanceId, "placement.drift", "world.item.drift", "scene.prototype").Succeeded, Is.True);

                PersistenceParticipantPrepareResult rejected = participant.PreparePayload(capture.PayloadJson, PlayerInventoryEquipmentPersistenceParticipant.CurrentParticipantSchemaVersion);

                Assert.That(rejected.Succeeded, Is.False);
                Assert.That(rejected.Message, Does.Contain("projection"));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void InventoryEquipmentProjectionDetectsDivergentIdentityLocation()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            string swordId = ItemInstanceId.Generate();
            PlayerInventoryEquipmentSaveData projection = new PlayerInventoryEquipmentSaveData
            {
                inventory = new InventorySaveData
                {
                    slotCapacity = 1,
                    entries =
                    {
                        new InventoryEntrySaveData { mode = InventoryEntrySaveMode.StatefulInstance, definitionId = sword.ItemId, itemInstanceId = swordId, quantity = 1 }
                    }
                },
                equipment = new EquipmentSaveData()
            };
            ItemIdentityInventoryBridgeResult identityProjection = ItemIdentityInventoryBridge.BuildInventoryEquipmentProjection(projection, registry, "person.prototype.player", "test.divergent");
            Assert.That(identityProjection.Succeeded, Is.True, identityProjection.Message);

            identityProjection.SaveData.records[0].location = new ItemLocationStateData { kind = ItemLocationKind.WorldPlacement, worldPlacementId = "placement.test", worldEntityId = "world.item.test" };
            ItemIdentityInventoryBridgeResult audit = ItemIdentityInventoryBridge.ValidateInventoryEquipmentProjection(projection, identityProjection.SaveData, "person.prototype.player");

            Assert.That(audit.Succeeded, Is.False);
            Assert.That(audit.Status, Is.EqualTo("ProjectionMismatch"));
        }

        [Test]
        public void ItemIdentityStackPolicyRejectsInstanceSpecificDifferences()
        {
            ItemDefinition potion = LoadItem("item.health-potion", out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string first = runtime.CreateItem(potion, ItemInstanceClassification.Fungible, ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            string second = runtime.CreateItem(potion, ItemInstanceClassification.Fungible, ownerPersonId: "person.owner").Snapshot.ItemInstanceId;
            Assert.That(runtime.TryGetSnapshot(first, out ItemInstanceSnapshot cleanA), Is.True);
            Assert.That(runtime.TryGetSnapshot(second, out ItemInstanceSnapshot cleanB), Is.True);
            Assert.That(ItemIdentityInventoryBridge.CanShareStack(cleanA, cleanB), Is.True);

            Assert.That(runtime.Rename(second, "Named Potion").Succeeded, Is.True);
            Assert.That(runtime.TryGetSnapshot(second, out ItemInstanceSnapshot named), Is.True);
            Assert.That(ItemIdentityInventoryBridge.CanShareStack(cleanA, named), Is.False);
        }

        [Test]
        public void PartialStackConsumptionPreservesRemainderAndCreatesExactConsumedIdentity()
        {
            ItemDefinition potion = LoadItem("item.health-potion", out _);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            string sourceId = ItemInstanceId.Generate();
            string consumedId = ItemInstanceId.Generate();

            ItemInstanceOperationResult created = runtime.CreateItem(
                potion,
                ItemInstanceClassification.StackableWhileEquivalent,
                sourceId,
                ownerPersonId: "person.owner",
                stackQuantity: 8);
            ItemInstanceOperationResult consumed = runtime.ConsumeStackQuantity(sourceId, 3, consumedId);

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(consumed.Succeeded, Is.True, consumed.Message);
            Assert.That(runtime.TryGetSnapshot(sourceId, out ItemInstanceSnapshot remainder), Is.True);
            Assert.That(remainder.StackQuantity, Is.EqualTo(5));
            Assert.That(runtime.TryGetSnapshot(consumedId, out ItemInstanceSnapshot removed), Is.True);
            Assert.That(removed.StackQuantity, Is.EqualTo(3));
            Assert.That(removed.LifecycleState, Is.EqualTo(ItemLifecycleState.Consumed));
            Assert.That(removed.Data.provenance.parentItemInstanceIds, Does.Contain(sourceId));
        }

        [Test]
        public void ProvenanceValidationRejectsUnknownReferencesCyclesAndDuplicateSerials()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            string first = ItemInstanceId.Generate();
            string second = ItemInstanceId.Generate();
            ItemInstanceRuntimeSaveData saveData = new ItemInstanceRuntimeSaveData
            {
                schemaVersion = ItemInstanceRuntimeSaveData.CurrentSchemaVersion,
                records =
                {
                    BaseRecord(sword, first),
                    BaseRecord(sword, second)
                }
            };

            saveData.records[0].provenance.parentItemInstanceIds = new[] { second };
            saveData.records[1].provenance.parentItemInstanceIds = new[] { first };
            Assert.That(ItemInstanceIdentityRuntime.ValidateSaveData(saveData, registry, out string cycleFailure), Is.False);
            Assert.That(cycleFailure, Does.Contain("cycle"));

            saveData.records[0].provenance.parentItemInstanceIds = new[] { ItemInstanceId.Generate() };
            saveData.records[1].provenance.parentItemInstanceIds = System.Array.Empty<string>();
            Assert.That(ItemInstanceIdentityRuntime.ValidateSaveData(saveData, registry, out string unknownFailure), Is.False);
            Assert.That(unknownFailure, Does.Contain("unknown parent"));

            saveData.records[0].provenance.parentItemInstanceIds = System.Array.Empty<string>();
            saveData.records[0].labels.serialNumber = "SERIAL-1";
            saveData.records[1].labels.serialNumber = "SERIAL-1";
            Assert.That(ItemInstanceIdentityRuntime.ValidateSaveData(saveData, registry, out string serialFailure), Is.False);
            Assert.That(serialFailure, Does.Contain("Duplicate item serial"));
        }

        [Test]
        public void RepresentativeHistoryIntegrationReferencesExactItemAndFailedPrepareDoesNotMutate()
        {
            ItemDefinition sword = LoadItem(SwordId, out DefinitionRegistry registry);
            ItemInstanceIdentityRuntime runtime = new ItemInstanceIdentityRuntime();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(registry, "world.prototype", new[] { "person.owner", "person.new-owner" });
            string id = runtime.CreateItem(sword, ownerPersonId: "person.owner").Snapshot.ItemInstanceId;

            ItemIdentityHistoryResult creation = ItemIdentityHistoryIntegration.RecordItemEvent(
                runtime,
                history,
                "tx.item.creation",
                "history-event.person-participation",
                "event.item.creation",
                id,
                "person.owner",
                ItemIdentityHistoryEventKind.Created,
                1d);

            Assert.That(creation.Succeeded, Is.True, creation.Message);
            Assert.That(creation.HistoryResult.Event.Payload.itemId, Is.EqualTo(id));

            ItemInstanceSnapshot before = runtime.QueryByOwner("person.owner")[0];
            ItemInstanceOperationResult rejected = ItemIdentityHistoryIntegration.TransferOwnershipWithRequiredHistory(
                runtime,
                history,
                "tx.item.bad-transfer",
                "history-event.missing",
                "event.item.bad-transfer",
                id,
                "person.owner",
                "person.new-owner",
                2d);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.TryGetSnapshot(id, out ItemInstanceSnapshot after), Is.True);
            Assert.That(after.OwnerPersonId, Is.EqualTo(before.OwnerPersonId));
        }

        private static ItemInstanceRecordData BaseRecord(ItemDefinition item, string instanceId)
        {
            return new ItemInstanceRecordData
            {
                itemInstanceId = instanceId,
                itemDefinitionId = item.ItemId,
                classification = ItemInstanceClassification.IndividuallyTracked,
                stackQuantity = 1,
                lifecycleState = ItemLifecycleState.Active,
                location = new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = "person.owner" },
                ownership = new ItemOwnershipStateData { kind = ItemOwnershipKind.PersonOwned, ownerPersonId = "person.owner" },
                labels = new ItemIdentityLabelData(),
                provenance = new ItemProvenanceData()
            };
        }

        private static ItemDefinition LoadItem(string itemId, out DefinitionRegistry registry)
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Prototype catalog is missing at {CatalogPath}.");
            registry = catalog.CreateRegistry();
            Assert.That(registry.TryGet(itemId, out ItemDefinition item), Is.True, itemId);
            return item;
        }
    }
}
