using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Inventory.Composition
{
    public static class ItemCompositionCoordinator
    {
        public static ItemCompositionCreationResult CreateItem(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            ItemCompositionCreationRequest request)
        {
            if (itemRuntime == null || compositionRuntime == null)
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.MissingRuntime, "Item identity and composition runtimes are required.");
            }

            if (request?.Definition == null)
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.MissingDefinition, "Item creation requires an item definition.");
            }

            ItemInstanceRuntimeSaveData originalItems = itemRuntime.CreateSaveData();
            ItemCompositionRuntimeSaveData originalCompositions = compositionRuntime.CreateSaveData();
            if (!TryCreateWorkingRuntimes(originalItems, originalCompositions, registry, out ItemInstanceIdentityRuntime workingItems, out ItemCompositionRuntime workingCompositions, out string failure))
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.ValidationFailed, failure);
            }

            ItemInstanceOperationResult create = workingItems.CreateItem(
                request.Definition,
                request.Classification,
                request.ItemInstanceId,
                request.CreatorPersonId,
                request.OwnerPersonId,
                request.CustodianPersonId,
                request.CreationSourceId);
            if (!create.Succeeded)
            {
                return ItemCompositionCreationResult.Failure(ToCompositionStatus(create.Status), create.Message);
            }

            ItemCompositionRecordData composition = ResolveRequestedComposition(request, registry, create.Snapshot, out bool compositionRequired);
            ItemCompositionOperationResult setComposition = null;
            if (composition != null)
            {
                setComposition = workingCompositions.SetComposition(workingItems, registry, composition, request.Purpose);
                if (!setComposition.Succeeded)
                {
                    return ItemCompositionCreationResult.Failure(setComposition.Status, setComposition.Message);
                }
            }
            else if (compositionRequired)
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.MissingMaterial, $"Item definition '{request.Definition.Id}' requires composition but no valid composition template or explicit composition was supplied.");
            }

            if (request.Preview)
            {
                return ItemCompositionCreationResult.Success(create.Snapshot, setComposition?.Snapshot, "Item and composition creation preview prepared.", preview: true);
            }

            return Commit(itemRuntime, compositionRuntime, registry, originalItems, originalCompositions, workingItems.CreateSaveData(), workingCompositions.CreateSaveData(), create.Snapshot.ItemInstanceId);
        }

        public static ItemCompositionOperationResult AttachTrackedComponent(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string parentItemInstanceId,
            string childItemInstanceId,
            ItemComponentEntryData component,
            ItemCompositionMutationPurpose purpose = ItemCompositionMutationPurpose.RuntimeGameplay)
        {
            if (itemRuntime == null || compositionRuntime == null)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingRuntime, "Item identity and composition runtimes are required.");
            }

            if (component == null || string.IsNullOrWhiteSpace(component.componentEntryId))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.InvalidRequest, "A tracked component entry is required.");
            }

            ItemInstanceRuntimeSaveData originalItems = itemRuntime.CreateSaveData();
            ItemCompositionRuntimeSaveData originalCompositions = compositionRuntime.CreateSaveData();
            if (!TryCreateWorkingRuntimes(originalItems, originalCompositions, registry, out ItemInstanceIdentityRuntime workingItems, out ItemCompositionRuntime workingCompositions, out string failure))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.ValidationFailed, failure);
            }

            ItemInstanceOperationResult reserve = workingItems.ReserveAsComponent(childItemInstanceId, parentItemInstanceId, component.componentEntryId);
            if (!reserve.Succeeded)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.InvalidComponentLocation, reserve.Message);
            }

            ItemCompositionRecordData record = workingCompositions.TryGetSnapshotForItem(parentItemInstanceId, out ItemCompositionSnapshot snapshot)
                ? snapshot.Data.Clone()
                : new ItemCompositionRecordData
                {
                    compositionId = $"item-composition.{parentItemInstanceId}",
                    itemInstanceId = parentItemInstanceId,
                    completeness = ItemCompositionCompleteness.Partial,
                    source = "composition.attach-component"
                };
            component = component.Clone();
            component.kind = ItemComponentKind.TrackedItemInstance;
            component.componentItemInstanceId = childItemInstanceId;
            record.components.RemoveAll(entry => string.Equals(entry.componentEntryId, component.componentEntryId, StringComparison.Ordinal));
            record.components.Add(component);
            ItemCompositionOperationResult set = workingCompositions.SetComposition(workingItems, registry, record, purpose);
            if (!set.Succeeded)
            {
                return set;
            }

            ItemCompositionCreationResult commit = Commit(itemRuntime, compositionRuntime, registry, originalItems, originalCompositions, workingItems.CreateSaveData(), workingCompositions.CreateSaveData(), parentItemInstanceId);
            return commit.Succeeded
                ? ItemCompositionOperationResult.Success(commit.Composition, "Tracked component attached.")
                : ItemCompositionOperationResult.Failure(commit.Status, commit.Message);
        }

        public static ItemCompositionOperationResult DetachTrackedComponentToInventory(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string parentItemInstanceId,
            string componentEntryId,
            string inventoryOwnerId,
            ItemCompositionMutationPurpose purpose = ItemCompositionMutationPurpose.RuntimeGameplay)
        {
            if (itemRuntime == null || compositionRuntime == null)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingRuntime, "Item identity and composition runtimes are required.");
            }

            ItemInstanceRuntimeSaveData originalItems = itemRuntime.CreateSaveData();
            ItemCompositionRuntimeSaveData originalCompositions = compositionRuntime.CreateSaveData();
            if (!TryCreateWorkingRuntimes(originalItems, originalCompositions, registry, out ItemInstanceIdentityRuntime workingItems, out ItemCompositionRuntime workingCompositions, out string failure))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.ValidationFailed, failure);
            }

            if (!workingCompositions.TryGetSnapshotForItem(parentItemInstanceId, out ItemCompositionSnapshot snapshot))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingItem, $"Parent item '{parentItemInstanceId}' has no composition.");
            }

            ItemCompositionRecordData record = snapshot.Data.Clone();
            ItemComponentEntryData component = record.components.FirstOrDefault(entry => string.Equals(entry.componentEntryId, componentEntryId, StringComparison.Ordinal));
            if (component == null || component.kind != ItemComponentKind.TrackedItemInstance)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.InvalidRequest, $"Composition component '{componentEntryId}' is not a tracked item component.");
            }

            ItemInstanceOperationResult release = workingItems.ReleaseComponentToInventory(component.componentItemInstanceId, inventoryOwnerId);
            if (!release.Succeeded)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.InvalidComponentLocation, release.Message);
            }

            record.components.RemoveAll(entry => string.Equals(entry.componentEntryId, componentEntryId, StringComparison.Ordinal));
            ItemCompositionOperationResult set = workingCompositions.SetComposition(workingItems, registry, record, purpose);
            if (!set.Succeeded)
            {
                return set;
            }

            ItemCompositionCreationResult commit = Commit(itemRuntime, compositionRuntime, registry, originalItems, originalCompositions, workingItems.CreateSaveData(), workingCompositions.CreateSaveData(), parentItemInstanceId);
            return commit.Succeeded
                ? ItemCompositionOperationResult.Success(commit.Composition, "Tracked component detached.")
                : ItemCompositionOperationResult.Failure(commit.Status, commit.Message);
        }

        private static ItemCompositionRecordData ResolveRequestedComposition(ItemCompositionCreationRequest request, DefinitionRegistry registry, ItemInstanceSnapshot item, out bool required)
        {
            required = request.RequireComposition;
            if (request.ExplicitComposition != null)
            {
                ItemCompositionRecordData explicitRecord = request.ExplicitComposition.Clone();
                explicitRecord.itemInstanceId = item.ItemInstanceId;
                explicitRecord.sourceItemDefinitionId = item.ItemDefinitionId;
                explicitRecord.lastMutationPurpose = request.Purpose;
                return explicitRecord;
            }

            if (request.UseDefaultTemplate
                && registry != null
                && registry.TryGet(item.ItemDefinitionId, out UnityIsekaiGame.Inventory.ItemDefinition definition)
                && definition.DefaultCompositionTemplate != null
                && !definition.DefaultCompositionTemplate.IsEmpty)
            {
                required |= definition.DefaultCompositionTemplate.required;
                ItemCompositionRecordData record = definition.DefaultCompositionTemplate.Instantiate(item.ItemInstanceId, item.ItemDefinitionId);
                record.lastMutationPurpose = request.Purpose;
                return record;
            }

            return required ? null : new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{item.ItemInstanceId}",
                itemInstanceId = item.ItemInstanceId,
                sourceItemDefinitionId = item.ItemDefinitionId,
                completeness = ItemCompositionCompleteness.Unknown,
                source = "composition.coordinator.unknown",
                lastMutationPurpose = request.Purpose,
                tags = new[] { "item.composition", "composition.unknown" }
            };
        }

        private static bool TryCreateWorkingRuntimes(
            ItemInstanceRuntimeSaveData itemSaveData,
            ItemCompositionRuntimeSaveData compositionSaveData,
            DefinitionRegistry registry,
            out ItemInstanceIdentityRuntime itemRuntime,
            out ItemCompositionRuntime compositionRuntime,
            out string failure)
        {
            itemRuntime = new ItemInstanceIdentityRuntime();
            compositionRuntime = new ItemCompositionRuntime();
            failure = string.Empty;
            ItemInstanceOperationResult itemRestore = itemRuntime.RestoreFromSaveData(itemSaveData, registry);
            if (!itemRestore.Succeeded)
            {
                failure = itemRestore.Message;
                return false;
            }

            ItemCompositionOperationResult compositionRestore = compositionRuntime.RestoreFromSaveData(compositionSaveData, registry, itemRuntime);
            if (!compositionRestore.Succeeded)
            {
                failure = compositionRestore.Message;
                return false;
            }

            return true;
        }

        private static ItemCompositionCreationResult Commit(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            ItemInstanceRuntimeSaveData originalItems,
            ItemCompositionRuntimeSaveData originalCompositions,
            ItemInstanceRuntimeSaveData preparedItems,
            ItemCompositionRuntimeSaveData preparedCompositions,
            string itemInstanceId)
        {
            if (!ItemInstanceIdentityRuntime.ValidateSaveData(preparedItems, registry, out string itemFailure))
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.ValidationFailed, itemFailure);
            }

            if (!ItemCompositionRuntime.ValidateSaveData(preparedCompositions, registry, itemRuntime: null, out string compositionFailure))
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.ValidationFailed, compositionFailure);
            }

            ItemInstanceOperationResult itemRestore = itemRuntime.RestoreFromSaveData(preparedItems, registry);
            if (!itemRestore.Succeeded)
            {
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.AtomicCommitFailed, itemRestore.Message);
            }

            ItemCompositionOperationResult compositionRestore = compositionRuntime.RestoreFromSaveData(preparedCompositions, registry, itemRuntime);
            if (!compositionRestore.Succeeded)
            {
                itemRuntime.RestoreFromSaveData(originalItems, registry);
                compositionRuntime.RestoreFromSaveData(originalCompositions, registry, itemRuntime);
                return ItemCompositionCreationResult.Failure(ItemCompositionOperationStatus.AtomicCommitFailed, compositionRestore.Message);
            }

            itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item);
            compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition);
            return ItemCompositionCreationResult.Success(item, composition, "Item and composition committed atomically.");
        }

        private static ItemCompositionOperationStatus ToCompositionStatus(ItemInstanceOperationStatus status)
        {
            return status switch
            {
                ItemInstanceOperationStatus.MissingDefinition => ItemCompositionOperationStatus.MissingDefinition,
                ItemInstanceOperationStatus.MissingItem => ItemCompositionOperationStatus.MissingItem,
                ItemInstanceOperationStatus.DuplicateItemInstanceId => ItemCompositionOperationStatus.DuplicateComposition,
                ItemInstanceOperationStatus.InvalidLocation => ItemCompositionOperationStatus.InvalidComponentLocation,
                ItemInstanceOperationStatus.RestoreFailed => ItemCompositionOperationStatus.RestoreFailed,
                _ => ItemCompositionOperationStatus.InvalidRequest
            };
        }
    }
}
