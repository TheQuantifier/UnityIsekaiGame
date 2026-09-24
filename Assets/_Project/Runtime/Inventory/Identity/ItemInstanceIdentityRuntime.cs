using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Identity
{
    public sealed class ItemInstanceIdentityRuntime
    {
        private readonly Dictionary<string, ItemInstanceRecordData> recordsById = new Dictionary<string, ItemInstanceRecordData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int Count => recordsById.Count;

        public IReadOnlyList<ItemInstanceSnapshot> Snapshots => recordsById.Values
            .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
            .Select(record => new ItemInstanceSnapshot(record))
            .ToArray();

        public ItemInstanceOperationResult CreateItem(
            IInventoryItemDefinition definition,
            ItemInstanceClassification classification = ItemInstanceClassification.IndividuallyTracked,
            string itemInstanceId = "",
            string creatorPersonId = "",
            string ownerPersonId = "",
            string custodianPersonId = "",
            string creationSourceId = "",
            bool preview = false,
            int stackQuantity = 1)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.MissingDefinition, "Cannot create an item instance without a definition.");
            }

            string resolvedId = string.IsNullOrWhiteSpace(itemInstanceId) ? ItemInstanceId.Generate() : itemInstanceId;
            if (!ItemInstanceId.IsValid(resolvedId) || string.IsNullOrWhiteSpace(resolvedId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Item instance ID '{resolvedId}' is not a canonical GUID string.");
            }

            if (recordsById.ContainsKey(resolvedId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.DuplicateItemInstanceId, $"Item instance '{resolvedId}' already exists.");
            }

            int resolvedQuantity = Math.Max(1, stackQuantity);
            if (classification is ItemInstanceClassification.Unique or ItemInstanceClassification.Serialized or ItemInstanceClassification.IndividuallyTracked
                && resolvedQuantity != 1)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, "Individually tracked item instances must have a stack quantity of one.");
            }

            ItemInstanceRecordData record = new ItemInstanceRecordData
            {
                itemInstanceId = resolvedId,
                itemDefinitionId = definition.Id,
                classification = classification,
                stackQuantity = resolvedQuantity,
                lifecycleState = string.IsNullOrWhiteSpace(custodianPersonId) ? ItemLifecycleState.Active : ItemLifecycleState.InInventory,
                ownership = new ItemOwnershipStateData
                {
                    kind = string.IsNullOrWhiteSpace(ownerPersonId) ? ItemOwnershipKind.Unowned : ItemOwnershipKind.PersonOwned,
                    ownerPersonId = ownerPersonId ?? string.Empty,
                    originalOwnerId = ownerPersonId ?? string.Empty,
                    custodianPersonId = custodianPersonId ?? string.Empty
                },
                location = new ItemLocationStateData
                {
                    kind = string.IsNullOrWhiteSpace(custodianPersonId) ? ItemLocationKind.Unassigned : ItemLocationKind.Inventory,
                    inventoryOwnerId = custodianPersonId ?? string.Empty
                },
                labels = new ItemIdentityLabelData
                {
                    originalName = definition.DisplayName
                },
                provenance = new ItemProvenanceData
                {
                    provenanceRootId = $"item-provenance.{resolvedId}",
                    creatorPersonId = creatorPersonId ?? string.Empty,
                    creationSourceId = creationSourceId ?? string.Empty
                },
                revision = 1L
            };

            if (!ValidateRecord(record, out string failureReason))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, failureReason);
            }

            if (!preview)
            {
                recordsById.Add(record.itemInstanceId, record);
                revision++;
            }

            return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(record), "Item instance created.", preview);
        }

        public bool TryGetSnapshot(string itemInstanceId, out ItemInstanceSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(itemInstanceId) && recordsById.TryGetValue(itemInstanceId, out ItemInstanceRecordData record))
            {
                snapshot = new ItemInstanceSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<ItemInstanceSnapshot> QueryByDefinition(string itemDefinitionId)
        {
            return recordsById.Values
                .Where(record => string.Equals(record.itemDefinitionId, itemDefinitionId, StringComparison.Ordinal))
                .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
                .Select(record => new ItemInstanceSnapshot(record))
                .ToArray();
        }

        public IReadOnlyList<ItemInstanceSnapshot> QueryByOwner(string ownerPersonId)
        {
            return recordsById.Values
                .Where(record => string.Equals(record.ownership?.ownerPersonId, ownerPersonId, StringComparison.Ordinal))
                .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
                .Select(record => new ItemInstanceSnapshot(record))
                .ToArray();
        }

        public ItemInstanceOperationResult Rename(string itemInstanceId, string customName)
        {
            return Mutate(itemInstanceId, record =>
            {
                record.labels ??= new ItemIdentityLabelData();
                record.labels.customName = customName ?? string.Empty;
            }, "Item renamed.");
        }

        public ItemInstanceOperationResult AssignMakerMarkAndSerial(string itemInstanceId, string makerMark, string serialNumber)
        {
            return Mutate(itemInstanceId, record =>
            {
                record.labels ??= new ItemIdentityLabelData();
                record.labels.makerMark = makerMark ?? string.Empty;
                record.labels.serialNumber = serialNumber ?? string.Empty;
                record.labels.attribution = string.IsNullOrWhiteSpace(makerMark) ? ItemAttributionStatus.Unknown : ItemAttributionStatus.Claimed;
            }, "Item maker mark and serial updated.");
        }

        public ItemInstanceOperationResult TransferOwnership(string itemInstanceId, ItemOwnershipKind ownershipKind, string ownerPersonId = "", string ownerOrganizationId = "", string disputeId = "")
        {
            if (ownershipKind == ItemOwnershipKind.PersonOwned && string.IsNullOrWhiteSpace(ownerPersonId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidOwnership, "Person ownership requires an owner Person ID.");
            }

            if (ownershipKind == ItemOwnershipKind.OrganizationOwned && string.IsNullOrWhiteSpace(ownerOrganizationId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidOwnership, "Organization ownership requires an organization ID.");
            }

            if (ownershipKind == ItemOwnershipKind.Disputed && string.IsNullOrWhiteSpace(disputeId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidOwnership, "Disputed ownership requires a dispute ID.");
            }

            return Mutate(itemInstanceId, record =>
            {
                record.ownership ??= new ItemOwnershipStateData();
                string previousOwner = record.ownership.ownerPersonId;
                if (!string.IsNullOrWhiteSpace(previousOwner))
                {
                    record.provenance ??= new ItemProvenanceData();
                    record.provenance.priorOwnerIds = AddDistinct(record.provenance.priorOwnerIds, previousOwner);
                }

                record.ownership.kind = ownershipKind;
                record.ownership.ownerPersonId = ownerPersonId ?? string.Empty;
                record.ownership.ownerOrganizationId = ownerOrganizationId ?? string.Empty;
                record.ownership.disputeId = disputeId ?? string.Empty;
                record.ownership.legalOwnerId = !string.IsNullOrWhiteSpace(ownerPersonId) ? ownerPersonId : ownerOrganizationId ?? string.Empty;
            }, "Item ownership updated.");
        }

        public ItemInstanceOperationResult TransferCustody(string itemInstanceId, string custodianPersonId = "", string custodianActorId = "", string custodianContainerId = "")
        {
            if (string.IsNullOrWhiteSpace(custodianPersonId) && string.IsNullOrWhiteSpace(custodianActorId) && string.IsNullOrWhiteSpace(custodianContainerId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, "Custody transfer requires a Person, Actor, or container ID.");
            }

            return Mutate(itemInstanceId, record =>
            {
                record.ownership ??= new ItemOwnershipStateData();
                string previousCustodian = FirstNonEmpty(record.ownership.custodianPersonId, record.ownership.custodianActorId, record.ownership.custodianContainerId);
                if (!string.IsNullOrWhiteSpace(previousCustodian))
                {
                    record.provenance ??= new ItemProvenanceData();
                    record.provenance.priorCustodianIds = AddDistinct(record.provenance.priorCustodianIds, previousCustodian);
                }

                record.ownership.custodianPersonId = custodianPersonId ?? string.Empty;
                record.ownership.custodianActorId = custodianActorId ?? string.Empty;
                record.ownership.custodianContainerId = custodianContainerId ?? string.Empty;
            }, "Item custody updated.");
        }

        public ItemInstanceOperationResult TransferStackQuantity(
            string sourceItemInstanceId,
            int quantity,
            string destinationOwnerPersonId,
            string destinationCustodianPersonId,
            string destinationInventoryOwnerId,
            string createdItemInstanceId = "",
            bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(sourceItemInstanceId) || !recordsById.TryGetValue(sourceItemInstanceId, out ItemInstanceRecordData source))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.MissingItem, $"Item instance '{sourceItemInstanceId}' was not found.");
            }

            if (quantity <= 0)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, "Stack transfer quantity must be positive.");
            }

            if (source.lifecycleState == ItemLifecycleState.Destroyed || source.lifecycleState == ItemLifecycleState.Consumed)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidState, $"Item instance '{sourceItemInstanceId}' is {source.lifecycleState}.");
            }

            if (quantity > source.stackQuantity)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Stack transfer requested {quantity}, but only {source.stackQuantity} is available.");
            }

            if (string.IsNullOrWhiteSpace(destinationOwnerPersonId) || string.IsNullOrWhiteSpace(destinationCustodianPersonId) || string.IsNullOrWhiteSpace(destinationInventoryOwnerId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, "Stack transfer requires destination owner, custodian, and inventory owner IDs.");
            }

            if (quantity == source.stackQuantity)
            {
                if (preview)
                {
                    ItemInstanceRecordData previewRecord = source.Clone();
                    ApplyStackDestination(previewRecord, destinationOwnerPersonId, destinationCustodianPersonId, destinationInventoryOwnerId);
                    previewRecord.revision = Math.Max(1L, source.revision + 1L);
                    return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(previewRecord), "Full stack transfer preview succeeded.", preview: true);
                }

                return Mutate(sourceItemInstanceId, record => ApplyStackDestination(record, destinationOwnerPersonId, destinationCustodianPersonId, destinationInventoryOwnerId), "Full item stack transferred.");
            }

            string newItemInstanceId = string.IsNullOrWhiteSpace(createdItemInstanceId) ? ItemInstanceId.Generate() : createdItemInstanceId;
            if (!ItemInstanceId.IsValid(newItemInstanceId) || recordsById.ContainsKey(newItemInstanceId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Split stack destination item instance ID '{newItemInstanceId}' is invalid or already exists.");
            }

            ItemInstanceRecordData remaining = source.Clone();
            ItemInstanceRecordData split = source.Clone();
            remaining.stackQuantity -= quantity;
            remaining.revision = Math.Max(1L, source.revision + 1L);
            split.itemInstanceId = newItemInstanceId;
            split.stackQuantity = quantity;
            split.provenance ??= new ItemProvenanceData();
            split.provenance.parentItemInstanceIds = AddDistinct(split.provenance.parentItemInstanceIds, sourceItemInstanceId);
            split.provenance.sourceItemInstanceIds = AddDistinct(split.provenance.sourceItemInstanceIds, sourceItemInstanceId);
            ApplyStackDestination(split, destinationOwnerPersonId, destinationCustodianPersonId, destinationInventoryOwnerId);
            split.revision = 1L;

            if (!ValidateRecord(remaining, out string remainingFailure))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, remainingFailure);
            }

            if (!ValidateRecord(split, out string splitFailure))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, splitFailure);
            }

            if (preview)
            {
                return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(split), "Partial stack transfer preview succeeded.", preview: true);
            }

            recordsById[sourceItemInstanceId] = remaining;
            recordsById.Add(newItemInstanceId, split);
            revision++;
            return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(split), "Partial stack quantity transferred.");
        }

        public ItemInstanceOperationResult SetContainerLocation(string itemInstanceId, string containerId)
        {
            if (string.IsNullOrWhiteSpace(containerId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "Container location requires a container ID.");
            }

            return SetLocation(itemInstanceId, new ItemLocationStateData { kind = ItemLocationKind.Container, containerId = containerId });
        }

        public ItemInstanceOperationResult SetInventoryLocation(string itemInstanceId, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "Inventory location requires an owner ID.");
            }

            return SetLocation(itemInstanceId, new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = ownerId });
        }

        public ItemInstanceOperationResult SetEquippedLocation(string itemInstanceId, string equipmentHolderId, string slotId)
        {
            if (string.IsNullOrWhiteSpace(equipmentHolderId) || string.IsNullOrWhiteSpace(slotId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "Equipped location requires holder and slot IDs.");
            }

            return SetLocation(itemInstanceId, new ItemLocationStateData { kind = ItemLocationKind.Equipped, equipmentHolderId = equipmentHolderId, equipmentSlotId = slotId });
        }

        public ItemInstanceOperationResult ReserveAsComponent(string itemInstanceId, string parentItemInstanceId, string componentEntryId)
        {
            if (string.IsNullOrWhiteSpace(parentItemInstanceId) || string.IsNullOrWhiteSpace(componentEntryId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "Component reservation requires parent item and component entry IDs.");
            }

            if (string.Equals(itemInstanceId, parentItemInstanceId, StringComparison.Ordinal))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "An item cannot be reserved as a component of itself.");
            }

            return SetLocation(itemInstanceId, new ItemLocationStateData
            {
                kind = ItemLocationKind.ProductionReserved,
                containerId = parentItemInstanceId,
                transitId = componentEntryId
            });
        }

        public ItemInstanceOperationResult ReleaseComponentToInventory(string itemInstanceId, string inventoryOwnerId)
        {
            if (string.IsNullOrWhiteSpace(inventoryOwnerId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "Detached component inventory location requires an owner ID.");
            }

            return SetInventoryLocation(itemInstanceId, inventoryOwnerId);
        }

        public ItemInstanceOperationResult ReleaseComponentToWorld(string itemInstanceId, string placementId, string worldEntityId, string sceneKey)
        {
            return SetWorldPlacement(itemInstanceId, placementId, worldEntityId, sceneKey);
        }

        public ItemInstanceOperationResult SetWorldPlacement(string itemInstanceId, string placementId, string worldEntityId, string sceneKey)
        {
            if (string.IsNullOrWhiteSpace(placementId) || string.IsNullOrWhiteSpace(worldEntityId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, "World placement requires placement and world entity IDs.");
            }

            if (recordsById.Values.Any(record => !string.Equals(record.itemInstanceId, itemInstanceId, StringComparison.Ordinal) && string.Equals(record.location?.worldPlacementId, placementId, StringComparison.Ordinal)))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, $"World placement ID '{placementId}' is already assigned.");
            }

            return SetLocation(itemInstanceId, new ItemLocationStateData { kind = ItemLocationKind.WorldPlacement, worldPlacementId = placementId, worldEntityId = worldEntityId, sceneKey = sceneKey ?? string.Empty });
        }

        public ItemInstanceOperationResult MarkLost(string itemInstanceId)
        {
            return Mutate(itemInstanceId, record =>
            {
                record.lifecycleState = ItemLifecycleState.Lost;
                record.location = new ItemLocationStateData { kind = ItemLocationKind.Unassigned };
            }, "Item marked lost.");
        }

        public ItemInstanceOperationResult Recover(string itemInstanceId, string custodianPersonId)
        {
            ItemInstanceOperationResult custody = TransferCustody(itemInstanceId, custodianPersonId);
            if (!custody.Succeeded)
            {
                return custody;
            }

            return Mutate(itemInstanceId, record =>
            {
                record.lifecycleState = ItemLifecycleState.InInventory;
                record.location = new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = custodianPersonId ?? string.Empty };
            }, "Item recovered.");
        }

        public ItemInstanceOperationResult DestroyOrConsume(string itemInstanceId, bool consumed)
        {
            return Mutate(itemInstanceId, record =>
            {
                record.lifecycleState = consumed ? ItemLifecycleState.Consumed : ItemLifecycleState.Destroyed;
                record.location = new ItemLocationStateData { kind = consumed ? ItemLocationKind.Consumed : ItemLocationKind.Destroyed };
            }, consumed ? "Item consumed." : "Item destroyed.");
        }

        public ItemInstanceOperationResult MarkDisassembled(string itemInstanceId)
        {
            return Mutate(itemInstanceId, record =>
            {
                record.lifecycleState = ItemLifecycleState.Disassembled;
                record.location = new ItemLocationStateData { kind = ItemLocationKind.Destroyed };
            }, "Item disassembled.");
        }

        public ItemInstanceOperationResult ConsumeStackQuantity(string itemInstanceId, int quantity, string consumedItemInstanceId = "")
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId) || !recordsById.TryGetValue(itemInstanceId, out ItemInstanceRecordData source))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            if (quantity <= 0 || quantity > source.stackQuantity)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Cannot consume {quantity} from stack '{itemInstanceId}' containing {source.stackQuantity}.");
            }

            if (quantity == source.stackQuantity)
            {
                return DestroyOrConsume(itemInstanceId, consumed: true);
            }

            if (source.classification is ItemInstanceClassification.Unique or ItemInstanceClassification.Serialized or ItemInstanceClassification.IndividuallyTracked)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Item instance '{itemInstanceId}' cannot be partially consumed.");
            }

            string consumedId = string.IsNullOrWhiteSpace(consumedItemInstanceId) ? ItemInstanceId.Generate() : consumedItemInstanceId;
            if (!ItemInstanceId.IsValid(consumedId) || recordsById.ContainsKey(consumedId))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidRequest, $"Consumed stack identity '{consumedId}' is invalid or already exists.");
            }

            ItemInstanceRecordData remaining = source.Clone();
            remaining.stackQuantity -= quantity;
            remaining.revision = Math.Max(1L, remaining.revision + 1L);

            ItemInstanceRecordData consumed = source.Clone();
            consumed.itemInstanceId = consumedId;
            consumed.stackQuantity = quantity;
            consumed.lifecycleState = ItemLifecycleState.Consumed;
            consumed.location = new ItemLocationStateData { kind = ItemLocationKind.Consumed };
            consumed.provenance ??= new ItemProvenanceData();
            consumed.provenance.parentItemInstanceIds = AddDistinct(consumed.provenance.parentItemInstanceIds, itemInstanceId);
            consumed.provenance.sourceItemInstanceIds = AddDistinct(consumed.provenance.sourceItemInstanceIds, itemInstanceId);
            consumed.revision = 1L;

            if (!ValidateRecord(remaining, out string remainingFailure))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, remainingFailure);
            }

            if (!ValidateRecord(consumed, out string consumedFailure))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, consumedFailure);
            }

            recordsById[itemInstanceId] = remaining;
            recordsById.Add(consumedId, consumed);
            revision++;
            return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(consumed), "Stack quantity consumed.");
        }

        public ItemInstanceProjection Project(string itemInstanceId, ItemProjectionAudience audience, InformationAccessDecision decision = null)
        {
            if (!TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot snapshot))
            {
                return new ItemInstanceProjection(null, audience, redacted: false, denied: true, Array.Empty<string>(), Array.Empty<string>());
            }

            if (audience == ItemProjectionAudience.AuthoritativeInternal || audience == ItemProjectionAudience.PrivilegedDebug || decision == null)
            {
                return new ItemInstanceProjection(snapshot, audience, redacted: false, denied: false, AllProjectionFields, Array.Empty<string>());
            }

            bool denied = decision.Decision == InformationAccessDecisionKind.Denied || decision.Decision == InformationAccessDecisionKind.MissingAuthorization;
            bool redacted = denied || decision.Decision == InformationAccessDecisionKind.RedactedAccess || decision.Decision == InformationAccessDecisionKind.PartialAccess;
            return new ItemInstanceProjection(snapshot, audience, redacted, denied, decision.AllowedDetails, decision.RedactedDetails.Concat(decision.HiddenDetails).ToArray());
        }

        public ItemInstanceRuntimeSaveData CreateSaveData()
        {
            return new ItemInstanceRuntimeSaveData
            {
                schemaVersion = ItemInstanceRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = recordsById.Values.OrderBy(record => record.itemInstanceId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public ItemInstanceOperationResult RestoreFromSaveData(ItemInstanceRuntimeSaveData saveData, DefinitionRegistry registry)
        {
            if (!ValidateSaveData(saveData, registry, out string failureReason))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.RestoreFailed, failureReason);
            }

            Dictionary<string, ItemInstanceRecordData> restored = saveData.records
                .Select(record => record.Clone())
                .ToDictionary(record => record.itemInstanceId, StringComparer.Ordinal);

            recordsById.Clear();
            foreach (KeyValuePair<string, ItemInstanceRecordData> pair in restored.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                recordsById.Add(pair.Key, pair.Value);
            }

            revision = Math.Max(0L, saveData.revision);
            return ItemInstanceOperationResult.Success(null, "Item instance runtime restored.");
        }

        public static bool ValidateSaveData(ItemInstanceRuntimeSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Item instance save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ItemInstanceRuntimeSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported item instance schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failureReason = "Item instance runtime revision cannot be negative.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<ItemInstanceRecordData> records = saveData.records ?? new List<ItemInstanceRecordData>();
            foreach (ItemInstanceRecordData record in records)
            {
                if (!ValidateRecord(record, out failureReason))
                {
                    return false;
                }

                if (!ids.Add(record.itemInstanceId))
                {
                    failureReason = $"Duplicate item instance ID '{record.itemInstanceId}'.";
                    return false;
                }

                if (registry != null && !registry.TryGet(record.itemDefinitionId, out IInventoryItemDefinition _))
                {
                    failureReason = $"Item instance '{record.itemInstanceId}' references unknown item definition '{record.itemDefinitionId}'.";
                    return false;
                }
            }

            if (!ValidateRecordGraph(records, ids, out failureReason))
            {
                return false;
            }

            return true;
        }

        private ItemInstanceOperationResult SetLocation(string itemInstanceId, ItemLocationStateData location)
        {
            if (!ValidateLocation(location, out string failure))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, failure);
            }

            return Mutate(itemInstanceId, record =>
            {
                record.location = location.Clone();
                record.lifecycleState = LocationToLifecycle(location.kind);
            }, "Item location updated.");
        }

        private ItemInstanceOperationResult Mutate(string itemInstanceId, Action<ItemInstanceRecordData> mutation, string successMessage)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId) || !recordsById.TryGetValue(itemInstanceId, out ItemInstanceRecordData current))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            if (current.lifecycleState == ItemLifecycleState.Destroyed || current.lifecycleState == ItemLifecycleState.Consumed)
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidState, $"Item instance '{itemInstanceId}' is {current.lifecycleState}.");
            }

            ItemInstanceRecordData working = current.Clone();
            mutation(working);
            working.revision = Math.Max(1L, current.revision + 1L);
            if (!ValidateRecord(working, out string failureReason))
            {
                return ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, failureReason);
            }

            recordsById[itemInstanceId] = working;
            revision++;
            return ItemInstanceOperationResult.Success(new ItemInstanceSnapshot(working), successMessage);
        }

        private static bool ValidateRecord(ItemInstanceRecordData record, out string failureReason)
        {
            failureReason = string.Empty;
            if (record == null)
            {
                failureReason = "Item record is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.itemInstanceId) || !ItemInstanceId.IsValid(record.itemInstanceId))
            {
                failureReason = $"Item record has invalid instance ID '{record.itemInstanceId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.itemDefinitionId))
            {
                failureReason = $"Item instance '{record.itemInstanceId}' is missing an item definition ID.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ItemInstanceClassification), record.classification) ||
                !Enum.IsDefined(typeof(ItemLifecycleState), record.lifecycleState))
            {
                failureReason = $"Item instance '{record.itemInstanceId}' has invalid classification or lifecycle state.";
                return false;
            }

            if (record.stackQuantity <= 0)
            {
                failureReason = $"Item instance '{record.itemInstanceId}' has invalid stack quantity {record.stackQuantity}.";
                return false;
            }

            if (!ValidateLocation(record.location, out failureReason) ||
                !ValidateWorldRepresentation(record.worldRepresentation, out failureReason) ||
                !ValidateOwnership(record.ownership, out failureReason))
            {
                failureReason = $"Item instance '{record.itemInstanceId}': {failureReason}";
                return false;
            }

            ItemLocationKind locationKind = record.location?.kind ?? ItemLocationKind.Unassigned;
            if ((record.lifecycleState == ItemLifecycleState.Destroyed && locationKind != ItemLocationKind.Destroyed) ||
                (record.lifecycleState == ItemLifecycleState.Consumed && locationKind != ItemLocationKind.Consumed))
            {
                failureReason = $"Item instance '{record.itemInstanceId}' has lifecycle {record.lifecycleState} but location {locationKind}.";
                return false;
            }

            if (record.provenance != null)
            {
                if (Contains(record.provenance.parentItemInstanceIds, record.itemInstanceId) || Contains(record.provenance.sourceItemInstanceIds, record.itemInstanceId))
                {
                    failureReason = $"Item instance '{record.itemInstanceId}' references itself as a source item.";
                    return false;
                }
            }

            return true;
        }

        private static void ApplyStackDestination(ItemInstanceRecordData record, string ownerPersonId, string custodianPersonId, string inventoryOwnerId)
        {
            record.ownership ??= new ItemOwnershipStateData();
            record.ownership.kind = ItemOwnershipKind.PersonOwned;
            record.ownership.ownerPersonId = ownerPersonId ?? string.Empty;
            record.ownership.ownerOrganizationId = string.Empty;
            record.ownership.disputeId = string.Empty;
            record.ownership.legalOwnerId = ownerPersonId ?? string.Empty;
            record.ownership.custodianPersonId = custodianPersonId ?? string.Empty;
            record.ownership.custodianActorId = string.Empty;
            record.ownership.custodianContainerId = string.Empty;
            record.location = new ItemLocationStateData { kind = ItemLocationKind.Inventory, inventoryOwnerId = inventoryOwnerId ?? string.Empty };
            record.lifecycleState = ItemLifecycleState.InInventory;
        }

        private static bool ValidateRecordGraph(IReadOnlyList<ItemInstanceRecordData> records, HashSet<string> ids, out string failureReason)
        {
            failureReason = string.Empty;
            Dictionary<string, ItemInstanceRecordData> byId = records
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.itemInstanceId))
                .ToDictionary(record => record.itemInstanceId, StringComparer.Ordinal);
            HashSet<string> serials = new HashSet<string>(StringComparer.Ordinal);

            foreach (ItemInstanceRecordData record in records)
            {
                if (record == null)
                {
                    continue;
                }

                string serial = record.labels?.serialNumber;
                if (!string.IsNullOrWhiteSpace(serial) && !serials.Add(serial))
                {
                    failureReason = $"Duplicate item serial '{serial}' is not allowed in one item identity graph.";
                    return false;
                }

                ItemProvenanceData provenance = record.provenance;
                if (provenance == null)
                {
                    continue;
                }

                if (!TryParseOptionalTime(provenance.creationWorldTime, out double creationTime) ||
                    !TryParseOptionalTime(provenance.destroyedWorldTime, out double destroyedTime))
                {
                    failureReason = $"Item instance '{record.itemInstanceId}' has invalid provenance time values.";
                    return false;
                }

                if (!double.IsNaN(creationTime) && !double.IsNaN(destroyedTime) && creationTime > destroyedTime)
                {
                    failureReason = $"Item instance '{record.itemInstanceId}' was created after it was destroyed.";
                    return false;
                }

                foreach (string parentId in provenance.parentItemInstanceIds ?? Array.Empty<string>())
                {
                    if (!ids.Contains(parentId))
                    {
                        failureReason = $"Item instance '{record.itemInstanceId}' references unknown parent item '{parentId}'.";
                        return false;
                    }
                }

                foreach (string sourceId in provenance.sourceItemInstanceIds ?? Array.Empty<string>())
                {
                    if (!ids.Contains(sourceId))
                    {
                        failureReason = $"Item instance '{record.itemInstanceId}' references unknown source item '{sourceId}'.";
                        return false;
                    }
                }

                if (HasCycle(record.itemInstanceId, byId, parent: true, out string parentChain))
                {
                    failureReason = $"Item instance provenance parent cycle detected: {parentChain}.";
                    return false;
                }

                if (HasCycle(record.itemInstanceId, byId, parent: false, out string sourceChain))
                {
                    failureReason = $"Item instance provenance source cycle detected: {sourceChain}.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasCycle(string rootId, Dictionary<string, ItemInstanceRecordData> byId, bool parent, out string chain)
        {
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            List<string> path = new List<string>();
            string detectedChain = string.Empty;
            bool cycle = Visit(rootId);
            chain = detectedChain;
            return cycle;

            bool Visit(string id)
            {
                if (string.IsNullOrWhiteSpace(id) || !byId.TryGetValue(id, out ItemInstanceRecordData record))
                {
                    return false;
                }

                if (!visiting.Add(id))
                {
                    path.Add(id);
                    detectedChain = string.Join(" -> ", path);
                    return true;
                }

                path.Add(id);
                string[] next = parent
                    ? record.provenance?.parentItemInstanceIds
                    : record.provenance?.sourceItemInstanceIds;
                foreach (string child in next ?? Array.Empty<string>())
                {
                    if (Visit(child))
                    {
                        return true;
                    }
                }

                visiting.Remove(id);
                path.RemoveAt(path.Count - 1);
                return false;
            }
        }

        private static bool TryParseOptionalTime(string value, out double parsed)
        {
            parsed = double.NaN;
            return string.IsNullOrWhiteSpace(value) || double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed);
        }

        private static bool ValidateLocation(ItemLocationStateData location, out string failureReason)
        {
            failureReason = string.Empty;
            location ??= new ItemLocationStateData();
            if (!Enum.IsDefined(typeof(ItemLocationKind), location.kind))
            {
                failureReason = "Location kind is invalid.";
                return false;
            }

            int populated = CountPopulated(location.containerId, location.inventoryOwnerId, location.equipmentHolderId, location.worldPlacementId, location.transitId);
            if (populated > 1 && location.kind != ItemLocationKind.Equipped && location.kind != ItemLocationKind.ProductionReserved)
            {
                failureReason = "Item has multiple incompatible location references.";
                return false;
            }

            if (location.kind == ItemLocationKind.Equipped && (string.IsNullOrWhiteSpace(location.equipmentHolderId) || string.IsNullOrWhiteSpace(location.equipmentSlotId)))
            {
                failureReason = "Equipped item location requires holder and slot references.";
                return false;
            }

            if (location.kind == ItemLocationKind.WorldPlacement && (string.IsNullOrWhiteSpace(location.worldPlacementId) || string.IsNullOrWhiteSpace(location.worldEntityId)))
            {
                failureReason = "World-placed item requires placement and world entity references.";
                return false;
            }

            if (location.kind == ItemLocationKind.ProductionReserved && (string.IsNullOrWhiteSpace(location.containerId) || string.IsNullOrWhiteSpace(location.transitId)))
            {
                failureReason = "Component-reserved item location requires parent and component references.";
                return false;
            }

            return true;
        }

        private static bool ValidateWorldRepresentation(ItemWorldRepresentationData representation, out string failureReason)
        {
            failureReason = string.Empty;
            if (representation == null)
            {
                return true;
            }

            if (float.IsNaN(representation.defaultScale) || float.IsInfinity(representation.defaultScale) || representation.defaultScale <= 0f)
            {
                failureReason = "World representation default scale must be positive.";
                return false;
            }

            if (float.IsNaN(representation.groundOffset) || float.IsInfinity(representation.groundOffset))
            {
                failureReason = "World representation ground offset is invalid.";
                return false;
            }

            return true;
        }

        private static bool ValidateOwnership(ItemOwnershipStateData ownership, out string failureReason)
        {
            failureReason = string.Empty;
            ownership ??= new ItemOwnershipStateData();
            if (!Enum.IsDefined(typeof(ItemOwnershipKind), ownership.kind))
            {
                failureReason = "Ownership kind is invalid.";
                return false;
            }

            if (ownership.kind == ItemOwnershipKind.PersonOwned && string.IsNullOrWhiteSpace(ownership.ownerPersonId))
            {
                failureReason = "Person-owned item is missing owner Person ID.";
                return false;
            }

            if (ownership.kind == ItemOwnershipKind.OrganizationOwned && string.IsNullOrWhiteSpace(ownership.ownerOrganizationId))
            {
                failureReason = "Organization-owned item is missing organization ID.";
                return false;
            }

            if (ownership.kind == ItemOwnershipKind.Disputed && string.IsNullOrWhiteSpace(ownership.disputeId))
            {
                failureReason = "Disputed ownership is missing a dispute ID.";
                return false;
            }

            return true;
        }

        private static ItemLifecycleState LocationToLifecycle(ItemLocationKind kind)
        {
            return kind switch
            {
                ItemLocationKind.Container => ItemLifecycleState.Stored,
                ItemLocationKind.Inventory => ItemLifecycleState.InInventory,
                ItemLocationKind.Equipped => ItemLifecycleState.Equipped,
                ItemLocationKind.WorldPlacement => ItemLifecycleState.PlacedInWorld,
                ItemLocationKind.Transit => ItemLifecycleState.InTransit,
                ItemLocationKind.Reserved or ItemLocationKind.ProductionReserved => ItemLifecycleState.Reserved,
                ItemLocationKind.Destroyed => ItemLifecycleState.Destroyed,
                ItemLocationKind.Consumed => ItemLifecycleState.Consumed,
                _ => ItemLifecycleState.Active
            };
        }

        private static string[] AddDistinct(string[] values, string value)
        {
            return (values ?? Array.Empty<string>()).Concat(new[] { value }).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        }

        private static bool Contains(string[] values, string value)
        {
            return values != null && values.Any(candidate => string.Equals(candidate, value, StringComparison.Ordinal));
        }

        private static bool IsNormalized(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        private static int CountPopulated(params string[] values)
        {
            return values.Count(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static readonly string[] AllProjectionFields =
        {
            "identity",
            "definition",
            "lifecycle",
            "location",
            "ownership",
            "custody",
            "condition",
            "quality",
            "labels",
            "provenance",
            "access"
        };
    }
}
