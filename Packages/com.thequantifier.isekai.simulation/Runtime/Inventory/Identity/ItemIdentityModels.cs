using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Identity
{
    [Serializable]
    public sealed class ItemInstanceRecordData
    {
        public string itemInstanceId;
        public string itemDefinitionId;
        public ItemInstanceClassification classification = ItemInstanceClassification.IndividuallyTracked;
        public int stackQuantity = 1;
        public ItemLifecycleState lifecycleState = ItemLifecycleState.Created;
        public ItemLocationStateData location = new ItemLocationStateData();
        public ItemWorldRepresentationData worldRepresentation = new ItemWorldRepresentationData();
        public ItemOwnershipStateData ownership = new ItemOwnershipStateData();
        public ItemIdentityLabelData labels = new ItemIdentityLabelData();
        public ItemProvenanceData provenance = new ItemProvenanceData();
        public string accessPolicyId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;
        public bool quarantined;

        public ItemInstanceRecordData Clone()
        {
            return new ItemInstanceRecordData
            {
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                classification = classification,
                stackQuantity = Math.Max(1, stackQuantity),
                lifecycleState = lifecycleState,
                location = location?.Clone() ?? new ItemLocationStateData(),
                worldRepresentation = worldRepresentation?.Clone() ?? new ItemWorldRepresentationData(),
                ownership = ownership?.Clone() ?? new ItemOwnershipStateData(),
                labels = labels?.Clone() ?? new ItemIdentityLabelData(),
                provenance = provenance?.Clone() ?? new ItemProvenanceData(),
                accessPolicyId = accessPolicyId ?? string.Empty,
                tags = CloneArray(tags),
                revision = revision,
                quarantined = quarantined
            };
        }

        private static string[] CloneArray(string[] values)
        {
            return values == null ? Array.Empty<string>() : values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemWorldRepresentationData
    {
        public string representationId;
        public string prefabReference;
        public string addressableKey;
        public string interactionProfileId;
        public string physicsProfileId;
        public string colliderProfileId;
        public string persistenceProfileId;
        public string equipmentUseId;
        public string pickupAdapterId;
        public string validationProfileId;
        public string allowedPlacementSurfaceId;
        public string worldLayer;
        public string worldTag;
        public bool allowMultipleRepresentations;
        public bool triggerCollider = true;
        public bool physicalCollider;
        public bool movable;
        public float defaultScale = 1f;
        public float groundOffset;
        public float rotationX;
        public float rotationY;
        public float rotationZ;

        public ItemWorldRepresentationData Clone()
        {
            return new ItemWorldRepresentationData
            {
                representationId = representationId ?? string.Empty,
                prefabReference = prefabReference ?? string.Empty,
                addressableKey = addressableKey ?? string.Empty,
                interactionProfileId = interactionProfileId ?? string.Empty,
                physicsProfileId = physicsProfileId ?? string.Empty,
                colliderProfileId = colliderProfileId ?? string.Empty,
                persistenceProfileId = persistenceProfileId ?? string.Empty,
                equipmentUseId = equipmentUseId ?? string.Empty,
                pickupAdapterId = pickupAdapterId ?? string.Empty,
                validationProfileId = validationProfileId ?? string.Empty,
                allowedPlacementSurfaceId = allowedPlacementSurfaceId ?? string.Empty,
                worldLayer = worldLayer ?? string.Empty,
                worldTag = worldTag ?? string.Empty,
                allowMultipleRepresentations = allowMultipleRepresentations,
                triggerCollider = triggerCollider,
                physicalCollider = physicalCollider,
                movable = movable,
                defaultScale = defaultScale,
                groundOffset = groundOffset,
                rotationX = rotationX,
                rotationY = rotationY,
                rotationZ = rotationZ
            };
        }
    }

    [Serializable]
    public sealed class ItemLocationStateData
    {
        public ItemLocationKind kind = ItemLocationKind.Unassigned;
        public string containerId;
        public string inventoryOwnerId;
        public string equipmentHolderId;
        public string equipmentSlotId;
        public string worldPlacementId;
        public string worldEntityId;
        public string sceneKey;
        public string transitId;

        public ItemLocationStateData Clone()
        {
            return new ItemLocationStateData
            {
                kind = kind,
                containerId = containerId ?? string.Empty,
                inventoryOwnerId = inventoryOwnerId ?? string.Empty,
                equipmentHolderId = equipmentHolderId ?? string.Empty,
                equipmentSlotId = equipmentSlotId ?? string.Empty,
                worldPlacementId = worldPlacementId ?? string.Empty,
                worldEntityId = worldEntityId ?? string.Empty,
                sceneKey = sceneKey ?? string.Empty,
                transitId = transitId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemOwnershipStateData
    {
        public ItemOwnershipKind kind = ItemOwnershipKind.Unknown;
        public string ownerPersonId;
        public string ownerOrganizationId;
        public string sharedOwnershipId;
        public string disputeId;
        public string custodianPersonId;
        public string custodianActorId;
        public string custodianContainerId;
        public string originalOwnerId;
        public string legalOwnerId;

        public ItemOwnershipStateData Clone()
        {
            return new ItemOwnershipStateData
            {
                kind = kind,
                ownerPersonId = ownerPersonId ?? string.Empty,
                ownerOrganizationId = ownerOrganizationId ?? string.Empty,
                sharedOwnershipId = sharedOwnershipId ?? string.Empty,
                disputeId = disputeId ?? string.Empty,
                custodianPersonId = custodianPersonId ?? string.Empty,
                custodianActorId = custodianActorId ?? string.Empty,
                custodianContainerId = custodianContainerId ?? string.Empty,
                originalOwnerId = originalOwnerId ?? string.Empty,
                legalOwnerId = legalOwnerId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemIdentityLabelData
    {
        public string customName;
        public string originalName;
        public string makerMark;
        public string serialNumber;
        public string batchNumber;
        public string inscriptionReferenceId;
        public string ownerMark;
        public string organizationMark;
        public string authenticitySeal;
        public string publicDisplayLabel;
        public string hiddenInternalLabel;
        public ItemAuthenticityStatus authenticity = ItemAuthenticityStatus.Unknown;
        public ItemAttributionStatus attribution = ItemAttributionStatus.Unknown;

        public ItemIdentityLabelData Clone()
        {
            return new ItemIdentityLabelData
            {
                customName = customName ?? string.Empty,
                originalName = originalName ?? string.Empty,
                makerMark = makerMark ?? string.Empty,
                serialNumber = serialNumber ?? string.Empty,
                batchNumber = batchNumber ?? string.Empty,
                inscriptionReferenceId = inscriptionReferenceId ?? string.Empty,
                ownerMark = ownerMark ?? string.Empty,
                organizationMark = organizationMark ?? string.Empty,
                authenticitySeal = authenticitySeal ?? string.Empty,
                publicDisplayLabel = publicDisplayLabel ?? string.Empty,
                hiddenInternalLabel = hiddenInternalLabel ?? string.Empty,
                authenticity = authenticity,
                attribution = attribution
            };
        }
    }

    [Serializable]
    public sealed class ItemProvenanceData
    {
        public string provenanceRootId;
        public string creationEventId;
        public string creationSourceId;
        public string creatorPersonId;
        public string manufacturerOrganizationId;
        public string creationLocationId;
        public string creationWorldTime;
        public string destroyedWorldTime;
        public string productionBatchId;
        public string recipeSourceId;
        public string[] parentItemInstanceIds = Array.Empty<string>();
        public string[] sourceItemInstanceIds = Array.Empty<string>();
        public string[] priorOwnerIds = Array.Empty<string>();
        public string[] priorCustodianIds = Array.Empty<string>();
        public string[] transferEventIds = Array.Empty<string>();
        public string[] historyEventIds = Array.Empty<string>();
        public string[] recordIds = Array.Empty<string>();

        public ItemProvenanceData Clone()
        {
            return new ItemProvenanceData
            {
                provenanceRootId = provenanceRootId ?? string.Empty,
                creationEventId = creationEventId ?? string.Empty,
                creationSourceId = creationSourceId ?? string.Empty,
                creatorPersonId = creatorPersonId ?? string.Empty,
                manufacturerOrganizationId = manufacturerOrganizationId ?? string.Empty,
                creationLocationId = creationLocationId ?? string.Empty,
                creationWorldTime = creationWorldTime ?? string.Empty,
                destroyedWorldTime = destroyedWorldTime ?? string.Empty,
                productionBatchId = productionBatchId ?? string.Empty,
                recipeSourceId = recipeSourceId ?? string.Empty,
                parentItemInstanceIds = CloneIds(parentItemInstanceIds),
                sourceItemInstanceIds = CloneIds(sourceItemInstanceIds),
                priorOwnerIds = CloneIds(priorOwnerIds),
                priorCustodianIds = CloneIds(priorCustodianIds),
                transferEventIds = CloneIds(transferEventIds),
                historyEventIds = CloneIds(historyEventIds),
                recordIds = CloneIds(recordIds)
            };
        }

        private static string[] CloneIds(string[] ids)
        {
            return ids == null ? Array.Empty<string>() : ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemInstanceRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ItemInstanceRecordData> records = new List<ItemInstanceRecordData>();

        public ItemInstanceRuntimeSaveData Clone()
        {
            return new ItemInstanceRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<ItemInstanceRecordData>() : records.Select(record => record?.Clone()).Where(record => record != null).ToList()
            };
        }
    }

    public sealed class ItemInstanceSnapshot
    {
        public ItemInstanceSnapshot(ItemInstanceRecordData data)
        {
            Data = data?.Clone() ?? new ItemInstanceRecordData();
        }

        public ItemInstanceRecordData Data { get; }
        public string ItemInstanceId => Data.itemInstanceId ?? string.Empty;
        public string ItemDefinitionId => Data.itemDefinitionId ?? string.Empty;
        public ItemInstanceClassification Classification => Data.classification;
        public int StackQuantity => Math.Max(1, Data.stackQuantity);
        public ItemLifecycleState LifecycleState => Data.lifecycleState;
        public ItemLocationKind LocationKind => Data.location?.kind ?? ItemLocationKind.Unassigned;
        public ItemOwnershipKind OwnershipKind => Data.ownership?.kind ?? ItemOwnershipKind.Unknown;
        public string OwnerPersonId => Data.ownership?.ownerPersonId ?? string.Empty;
        public string CustodianPersonId => Data.ownership?.custodianPersonId ?? string.Empty;
        public string CustomName => Data.labels?.customName ?? string.Empty;
        public string MakerMark => Data.labels?.makerMark ?? string.Empty;
        public string SerialNumber => Data.labels?.serialNumber ?? string.Empty;
        public long Revision => Data.revision;
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ItemInformationSubject.Create(ItemInstanceId, ItemDefinitionId, OwnerPersonId, Tags);
        }
    }

    public sealed class ItemInstanceProjection
    {
        public ItemInstanceProjection(ItemInstanceSnapshot snapshot, ItemProjectionAudience audience, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
        {
            Snapshot = snapshot;
            Audience = audience;
            Redacted = redacted;
            Denied = denied;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public ItemInstanceSnapshot Snapshot { get; }
        public ItemProjectionAudience Audience { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class ItemInstanceOperationResult
    {
        private ItemInstanceOperationResult(bool succeeded, bool preview, ItemInstanceOperationStatus status, string message, ItemInstanceSnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public ItemInstanceOperationStatus Status { get; }
        public string Message { get; }
        public ItemInstanceSnapshot Snapshot { get; }

        public static ItemInstanceOperationResult Success(ItemInstanceSnapshot snapshot, string message = "Item operation succeeded.", bool preview = false)
        {
            return new ItemInstanceOperationResult(true, preview, preview ? ItemInstanceOperationStatus.Preview : ItemInstanceOperationStatus.Succeeded, message, snapshot);
        }

        public static ItemInstanceOperationResult Failure(ItemInstanceOperationStatus status, string message)
        {
            return new ItemInstanceOperationResult(false, false, status, message, null);
        }
    }

    public static class ItemInformationSubject
    {
        public const string ItemInstanceSubjectTag = "subject-type:item-instance";

        public static readonly string[] ProtectedFields =
        {
            "owner",
            "custodian",
            "serial",
            "maker",
            "provenance",
            "authenticity",
            "hidden-name",
            "access-policy",
            "secret-production-source"
        };

        public static InformationSubjectReferenceData Create(string itemInstanceId, string itemDefinitionId, string ownerPersonId = "", IEnumerable<string> tags = null)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.item", "item.instance", ItemInstanceSubjectTag })
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();

            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = itemInstanceId ?? string.Empty,
                parentSubjectId = itemDefinitionId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = subjectTags
            };
        }
    }
}
