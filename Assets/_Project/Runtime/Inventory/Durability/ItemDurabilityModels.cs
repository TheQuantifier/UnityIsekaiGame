using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Durability
{
    [Serializable]
    public sealed class ItemDamageChannelStateData
    {
        public ItemDamageChannel channel = ItemDamageChannel.GeneralWear;
        public float accumulatedDamage;
        public string lastSourceId;
        public string lastWorldTime;

        public ItemDamageChannelStateData Clone()
        {
            return new ItemDamageChannelStateData
            {
                channel = channel,
                accumulatedDamage = accumulatedDamage,
                lastSourceId = lastSourceId ?? string.Empty,
                lastWorldTime = lastWorldTime ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemComponentDurabilityData
    {
        public string componentEntryId;
        public float currentDurability;
        public float maximumDurability;
        public float originalMaximumDurability;
        public float permanentCapacityLoss;
        public ItemComponentCriticality criticality = ItemComponentCriticality.Functional;
        public ItemFunctionalState functionalState = ItemFunctionalState.FullyFunctional;
        public ItemBreakageState breakageState = ItemBreakageState.None;
        public string[] affectedMaterialEntryIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public ItemComponentDurabilityData Clone()
        {
            return new ItemComponentDurabilityData
            {
                componentEntryId = componentEntryId ?? string.Empty,
                currentDurability = currentDurability,
                maximumDurability = maximumDurability,
                originalMaximumDurability = originalMaximumDurability,
                permanentCapacityLoss = permanentCapacityLoss,
                criticality = criticality,
                functionalState = functionalState,
                breakageState = breakageState,
                affectedMaterialEntryIds = CloneIds(affectedMaterialEntryIds),
                tags = CloneIds(tags),
                revision = revision
            };
        }

        private static string[] CloneIds(IEnumerable<string> ids)
        {
            return (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemDurabilityRevisionData
    {
        public long revision;
        public string operationId;
        public string sourceId;
        public string message;
        public string worldTime;

        public ItemDurabilityRevisionData Clone()
        {
            return new ItemDurabilityRevisionData
            {
                revision = revision,
                operationId = operationId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                message = message ?? string.Empty,
                worldTime = worldTime ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemRepairRecordData
    {
        public string repairId;
        public string itemInstanceId;
        public string repairedComponentEntryId;
        public float recoveredDurability;
        public float permanentCapacityLossApplied;
        public ItemRepairQuality repairQuality = ItemRepairQuality.Adequate;
        public string actorPersonId;
        public string sourceId;
        public string worldTime;

        public ItemRepairRecordData Clone()
        {
            return new ItemRepairRecordData
            {
                repairId = repairId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                repairedComponentEntryId = repairedComponentEntryId ?? string.Empty,
                recoveredDurability = recoveredDurability,
                permanentCapacityLossApplied = permanentCapacityLossApplied,
                repairQuality = repairQuality,
                actorPersonId = actorPersonId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                worldTime = worldTime ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemDurabilityRecordData
    {
        public string durabilityRecordId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public string policyId = ItemDegradationPolicyDefinition.StandardPolicyId;
        public float currentDurability = 100f;
        public float maximumDurability = 100f;
        public float originalMaximumDurability = 100f;
        public float permanentCapacityLoss;
        public float recoverableDamage;
        public float irrecoverableDamage;
        public float wear;
        public int lastBreakCheckPercent = 11;
        public long breakCheckSequence;
        public bool hasBroken;
        public bool pendingForcedDecomposition;
        public string conditionBandId;
        public ItemFunctionalState functionalState = ItemFunctionalState.FullyFunctional;
        public ItemBreakageState breakageState = ItemBreakageState.None;
        public ItemMaintenanceState maintenanceState = ItemMaintenanceState.Maintained;
        public ItemDurabilityRecordSource source = ItemDurabilityRecordSource.Unknown;
        public long relatedItemRevision;
        public long relatedCompositionRevision;
        public long relatedQualityRevision;
        public string accessPolicyId;
        public string provenanceId;
        public string lastDamageWorldTime;
        public string lastRepairWorldTime;
        public List<ItemComponentDurabilityData> components = new List<ItemComponentDurabilityData>();
        public List<ItemDamageChannelStateData> damageChannels = new List<ItemDamageChannelStateData>();
        public List<ItemRepairRecordData> repairHistory = new List<ItemRepairRecordData>();
        public string[] tags = Array.Empty<string>();
        public List<ItemDurabilityRevisionData> revisionHistory = new List<ItemDurabilityRevisionData>();
        public long revision = 1L;

        public ItemDurabilityRecordData Clone()
        {
            return new ItemDurabilityRecordData
            {
                durabilityRecordId = durabilityRecordId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                currentDurability = currentDurability,
                maximumDurability = maximumDurability,
                originalMaximumDurability = originalMaximumDurability,
                permanentCapacityLoss = permanentCapacityLoss,
                recoverableDamage = recoverableDamage,
                irrecoverableDamage = irrecoverableDamage,
                wear = wear,
                lastBreakCheckPercent = lastBreakCheckPercent,
                breakCheckSequence = breakCheckSequence,
                hasBroken = hasBroken,
                pendingForcedDecomposition = pendingForcedDecomposition,
                conditionBandId = conditionBandId ?? string.Empty,
                functionalState = functionalState,
                breakageState = breakageState,
                maintenanceState = maintenanceState,
                source = source,
                relatedItemRevision = relatedItemRevision,
                relatedCompositionRevision = relatedCompositionRevision,
                relatedQualityRevision = relatedQualityRevision,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                lastDamageWorldTime = lastDamageWorldTime ?? string.Empty,
                lastRepairWorldTime = lastRepairWorldTime ?? string.Empty,
                components = components == null ? new List<ItemComponentDurabilityData>() : components.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                damageChannels = damageChannels == null ? new List<ItemDamageChannelStateData>() : damageChannels.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                repairHistory = repairHistory == null ? new List<ItemRepairRecordData>() : repairHistory.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                tags = CloneIds(tags),
                revisionHistory = revisionHistory == null ? new List<ItemDurabilityRevisionData>() : revisionHistory.Select(entry => entry?.Clone()).Where(entry => entry != null).OrderBy(entry => entry.revision).ThenBy(entry => entry.operationId, StringComparer.Ordinal).ToList(),
                revision = revision
            };
        }

        private static string[] CloneIds(IEnumerable<string> ids)
        {
            return (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemDurabilityRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 3;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ItemDurabilityRecordData> records = new List<ItemDurabilityRecordData>();

        public ItemDurabilityRuntimeSaveData Clone()
        {
            return new ItemDurabilityRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<ItemDurabilityRecordData>() : records.Select(record => record?.Clone()).Where(record => record != null).ToList()
            };
        }
    }

    public sealed class ItemDurabilitySnapshot
    {
        public ItemDurabilitySnapshot(ItemDurabilityRecordData data)
        {
            Data = data?.Clone() ?? new ItemDurabilityRecordData();
        }

        public ItemDurabilityRecordData Data { get; }
        public string DurabilityRecordId => Data.durabilityRecordId ?? string.Empty;
        public string ItemInstanceId => Data.itemInstanceId ?? string.Empty;
        public string ItemDefinitionId => Data.itemDefinitionId ?? string.Empty;
        public float CurrentDurability => Data.currentDurability;
        public float MaximumDurability => Data.maximumDurability;
        public float NormalizedDurability => MaximumDurability <= 0f ? 0f : CurrentDurability / MaximumDurability;
        public int LastBreakCheckPercent => Data.lastBreakCheckPercent;
        public bool HasBroken => Data.hasBroken;
        public bool PendingForcedDecomposition => Data.pendingForcedDecomposition;
        public string ConditionBandId => Data.conditionBandId ?? string.Empty;
        public ItemFunctionalState FunctionalState => Data.functionalState;
        public ItemBreakageState BreakageState => Data.breakageState;
        public long Revision => Data.revision;
        public IReadOnlyList<ItemComponentDurabilityData> Components => Data.components ?? new List<ItemComponentDurabilityData>();

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ItemDurabilityInformationSubject.Create(ItemInstanceId, DurabilityRecordId, ItemDefinitionId, Data.tags);
        }
    }

    public sealed class ItemDurabilityOperationResult
    {
        private ItemDurabilityOperationResult(bool succeeded, bool preview, ItemDurabilityOperationStatus status, string message, ItemDurabilitySnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public ItemDurabilityOperationStatus Status { get; }
        public string Message { get; }
        public ItemDurabilitySnapshot Snapshot { get; }
        public static ItemDurabilityOperationResult Success(ItemDurabilitySnapshot snapshot, string message = "Item durability operation succeeded.", bool preview = false)
        {
            return new ItemDurabilityOperationResult(true, preview, preview ? ItemDurabilityOperationStatus.Preview : ItemDurabilityOperationStatus.Succeeded, message, snapshot);
        }

        public static ItemDurabilityOperationResult Failure(ItemDurabilityOperationStatus status, string message)
        {
            return new ItemDurabilityOperationResult(false, false, status, message, null);
        }
    }

    public sealed class ItemDurabilityProjection
    {
        public ItemDurabilityProjection(ItemDurabilitySnapshot snapshot, bool denied, bool redacted, IReadOnlyList<ItemComponentDurabilityData> visibleComponents, IReadOnlyList<string> redactedFields)
        {
            Snapshot = snapshot;
            Denied = denied;
            Redacted = redacted;
            VisibleComponents = (visibleComponents ?? Array.Empty<ItemComponentDurabilityData>()).Select(entry => entry.Clone()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public ItemDurabilitySnapshot Snapshot { get; }
        public bool Denied { get; }
        public bool Redacted { get; }
        public IReadOnlyList<ItemComponentDurabilityData> VisibleComponents { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }
}
