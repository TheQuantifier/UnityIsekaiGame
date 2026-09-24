using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Durability
{
    public sealed class ItemDurabilityRuntime
    {
        private readonly Dictionary<string, ItemDurabilityRecordData> recordsById = new Dictionary<string, ItemDurabilityRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> recordIdByItemId = new Dictionary<string, string>(StringComparer.Ordinal);
        private ItemConditionScaleDefinition conditionScale;
        private long revision;

        public long Revision => revision;
        public int Count => recordsById.Count;
        public event Action<string> ItemDurabilityStateChanged;

        public IReadOnlyList<ItemDurabilitySnapshot> Snapshots => recordsById.Values
            .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
            .Select(record => new ItemDurabilitySnapshot(record))
            .ToArray();

        public bool TryGetDurabilityForItem(string itemInstanceId, out ItemDurabilitySnapshot snapshot)
        {
            snapshot = null;
            return !string.IsNullOrWhiteSpace(itemInstanceId)
                && recordIdByItemId.TryGetValue(itemInstanceId, out string recordId)
                && TryGetDurability(recordId, out snapshot);
        }

        public bool TryGetDurability(string durabilityRecordId, out ItemDurabilitySnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(durabilityRecordId) && recordsById.TryGetValue(durabilityRecordId, out ItemDurabilityRecordData record))
            {
                snapshot = new ItemDurabilitySnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public ItemDurabilityOperationResult EnsureDefaultDurability(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            bool preview = false)
        {
            ConfigureConditionScale(registry);
            if (TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot existing))
            {
                return ItemDurabilityOperationResult.Success(existing, "Item durability already exists.", preview);
            }

            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            float quality = ResolveQuality(item, qualityRuntime, registry);
            float materialDurability = ResolveMaterialDurability(itemInstanceId, compositionRuntime, registry);
            float max = Mathf.Clamp((100f * (0.75f + materialDurability * 0.5f)) * (0.75f + quality * 0.5f), 1f, 500f);
            float normalized = Mathf.Clamp01(registry?.Defaults?.InitialDurabilityNormalized ?? 1f);
            ItemDurabilityRecordData record = new ItemDurabilityRecordData
            {
                durabilityRecordId = RecordId(itemInstanceId),
                itemInstanceId = itemInstanceId,
                itemDefinitionId = item.ItemDefinitionId,
                currentDurability = Mathf.Clamp(max * normalized, 0f, max),
                maximumDurability = max,
                originalMaximumDurability = max,
                source = ItemDurabilityRecordSource.DefinitionDefault,
                relatedItemRevision = item.Revision,
                relatedCompositionRevision = compositionRuntime != null && compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition) ? composition.Revision : 0L,
                relatedQualityRevision = qualityRuntime != null && qualityRuntime.TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot qualitySnapshot) ? qualitySnapshot.Revision : 0L,
                tags = new[] { "item.durability", "durability.default" }
            };

            if (compositionRuntime != null && compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot compositionForComponents))
            {
                foreach (ItemComponentEntryData component in compositionForComponents.Components)
                {
                    if (component == null || string.IsNullOrWhiteSpace(component.componentEntryId))
                    {
                        continue;
                    }

                    record.components.Add(new ItemComponentDurabilityData
                    {
                        componentEntryId = component.componentEntryId,
                        currentDurability = record.currentDurability,
                        maximumDurability = record.maximumDurability,
                        originalMaximumDurability = record.originalMaximumDurability,
                        criticality = component.optional ? ItemComponentCriticality.Supporting : ItemComponentCriticality.Functional,
                        affectedMaterialEntryIds = component.materialEntryIds ?? Array.Empty<string>()
                    });
                }
            }

            EvaluateRecord(record);
            return SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record, preview);
        }

        public ItemDurabilityOperationResult SetDurabilityRecord(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            ItemDurabilityRecordData record,
            bool preview = false)
        {
            ConfigureConditionScale(registry);
            if (itemRuntime == null)
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.MissingRuntime, "Item identity runtime is missing.");
            }

            ItemDurabilityRecordData working = record?.Clone();
            NormalizeRecord(working);
            EvaluateRecord(working);
            if (!ValidateRecord(working, registry, itemRuntime, compositionRuntime, out string failure))
            {
                return ItemDurabilityOperationResult.Failure(ToStatus(failure), failure);
            }

            if (preview)
            {
                return ItemDurabilityOperationResult.Success(new ItemDurabilitySnapshot(working), "Item durability preview prepared.", true);
            }

            bool replacing = recordsById.TryGetValue(working.durabilityRecordId, out ItemDurabilityRecordData existing);
            if (recordIdByItemId.TryGetValue(working.itemInstanceId, out string existingForItem)
                && !string.Equals(existingForItem, working.durabilityRecordId, StringComparison.Ordinal))
            {
                recordsById.Remove(existingForItem);
            }

            working.revision = Math.Max(1L, replacing ? existing.revision + 1L : working.revision);
            AddRevision(working, replacing ? "durability.replace" : "durability.create", working.source.ToString(), replacing ? "Durability record replaced." : "Durability record created.");
            recordsById[working.durabilityRecordId] = working;
            recordIdByItemId[working.itemInstanceId] = working.durabilityRecordId;
            revision++;
            ItemDurabilityStateChanged?.Invoke(working.itemInstanceId);
            return ItemDurabilityOperationResult.Success(new ItemDurabilitySnapshot(working), "Item durability set.");
        }

        public ItemDurabilityOperationResult ApplyDamage(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            float amount,
            ItemDamageChannel channel = ItemDamageChannel.GeneralWear,
            string componentEntryId = "",
            string sourceId = "",
            bool permanent = false,
            bool preview = false)
        {
            if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.InvalidValue, "Durability damage must be a non-negative finite value.");
            }

            ItemDurabilityOperationResult ensured = EnsureDefaultDurability(itemRuntime, compositionRuntime, qualityRuntime, registry, itemInstanceId);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            ItemDurabilityRecordData record = ensured.Snapshot.Data.Clone();
            record.currentDurability = Mathf.Max(0f, record.currentDurability - amount);
            record.recoverableDamage = Mathf.Max(0f, record.maximumDurability - record.currentDurability);
            if (permanent)
            {
                float permanentLoss = amount * 0.25f;
                record.permanentCapacityLoss = Mathf.Min(record.originalMaximumDurability, record.permanentCapacityLoss + permanentLoss);
                record.maximumDurability = Mathf.Max(1f, record.originalMaximumDurability - record.permanentCapacityLoss);
                record.currentDurability = Mathf.Min(record.currentDurability, record.maximumDurability);
                record.irrecoverableDamage = record.permanentCapacityLoss;
            }

            if (!string.IsNullOrWhiteSpace(componentEntryId))
            {
                ItemComponentDurabilityData component = record.components.FirstOrDefault(entry => string.Equals(entry.componentEntryId, componentEntryId, StringComparison.Ordinal));
                if (component == null)
                {
                    return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.InvalidRequest, $"Durability component '{componentEntryId}' does not exist.");
                }

                component.currentDurability = Mathf.Max(0f, component.currentDurability - amount);
                component.revision++;
                EvaluateComponent(component);
            }

            ItemDamageChannelStateData channelState = record.damageChannels.FirstOrDefault(entry => entry.channel == channel);
            if (channelState == null)
            {
                channelState = new ItemDamageChannelStateData { channel = channel };
                record.damageChannels.Add(channelState);
            }

            channelState.accumulatedDamage += amount;
            channelState.lastSourceId = sourceId ?? string.Empty;
            record.lastDamageWorldTime = sourceId ?? string.Empty;
            record.source = ItemDurabilityRecordSource.Custom;
            EvaluateRecord(record);
            return SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record, preview);
        }

        public ItemDurabilityOperationResult ApplyWear(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            float amount,
            string sourceId = "",
            bool preview = false)
        {
            ItemDurabilityOperationResult result = ApplyDamage(itemRuntime, compositionRuntime, qualityRuntime, registry, itemInstanceId, amount, ItemDamageChannel.GeneralWear, sourceId: sourceId, preview: preview);
            if (result.Succeeded && result.Snapshot != null && !preview)
            {
                ItemDurabilityRecordData record = result.Snapshot.Data.Clone();
                record.wear = Mathf.Min(record.originalMaximumDurability, record.wear + amount);
                EvaluateRecord(record);
                return SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record);
            }

            return result;
        }

        public ItemDurabilityOperationResult Maintain(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            string sourceId = "",
            bool preview = false)
        {
            ItemDurabilityOperationResult ensured = EnsureDefaultDurability(itemRuntime, compositionRuntime, qualityRuntime, registry, itemInstanceId);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            ItemDurabilityRecordData record = ensured.Snapshot.Data.Clone();
            record.maintenanceState = ItemMaintenanceState.Maintained;
            record.wear = Mathf.Max(0f, record.wear * 0.9f);
            AddRevision(record, "durability.maintain", sourceId, "Item maintained.");
            EvaluateRecord(record);
            return SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record, preview);
        }

        public ItemDurabilityOperationResult Repair(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            float amount,
            ItemRepairQuality repairQuality = ItemRepairQuality.Adequate,
            string componentEntryId = "",
            string repairId = "",
            string actorPersonId = "",
            string sourceId = "",
            bool preview = false)
        {
            if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.InvalidValue, "Repair amount must be a non-negative finite value.");
            }

            ItemDurabilityOperationResult ensured = EnsureDefaultDurability(itemRuntime, compositionRuntime, qualityRuntime, registry, itemInstanceId);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            ItemDurabilityRecordData record = ensured.Snapshot.Data.Clone();
            float penalty = RepairPenalty(repairQuality, amount);
            record.permanentCapacityLoss = Mathf.Min(record.originalMaximumDurability - 1f, record.permanentCapacityLoss + penalty);
            record.maximumDurability = Mathf.Max(1f, record.originalMaximumDurability - record.permanentCapacityLoss);
            record.currentDurability = Mathf.Min(record.maximumDurability, record.currentDurability + amount);
            record.recoverableDamage = Mathf.Max(0f, record.maximumDurability - record.currentDurability);
            record.irrecoverableDamage = record.permanentCapacityLoss;
            if (!string.IsNullOrWhiteSpace(componentEntryId))
            {
                ItemComponentDurabilityData component = record.components.FirstOrDefault(entry => string.Equals(entry.componentEntryId, componentEntryId, StringComparison.Ordinal));
                if (component == null)
                {
                    return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.InvalidRequest, $"Durability component '{componentEntryId}' does not exist.");
                }

                component.permanentCapacityLoss = Mathf.Min(component.originalMaximumDurability - 1f, component.permanentCapacityLoss + penalty);
                component.maximumDurability = Mathf.Max(1f, component.originalMaximumDurability - component.permanentCapacityLoss);
                component.currentDurability = Mathf.Min(component.maximumDurability, component.currentDurability + amount);
                component.revision++;
                EvaluateComponent(component);
            }

            record.repairHistory.Add(new ItemRepairRecordData
            {
                repairId = string.IsNullOrWhiteSpace(repairId) ? $"repair.{record.itemInstanceId}.{record.revision + 1L}" : repairId,
                itemInstanceId = itemInstanceId,
                repairedComponentEntryId = componentEntryId ?? string.Empty,
                recoveredDurability = amount,
                permanentCapacityLossApplied = penalty,
                repairQuality = repairQuality,
                actorPersonId = actorPersonId ?? string.Empty,
                sourceId = sourceId ?? string.Empty
            });
            record.source = ItemDurabilityRecordSource.Repair;
            EvaluateRecord(record);
            return SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record, preview);
        }

        public ItemDurabilityOperationResult PreviewSalvage(string itemInstanceId)
        {
            if (!TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot snapshot))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.MissingDurability, $"Item durability for '{itemInstanceId}' was not found.");
            }

            List<ItemSalvageOutputData> outputs = BuildSalvageOutputs(snapshot.Data);
            return ItemDurabilityOperationResult.Success(snapshot, "Salvage preview prepared.", true, outputs);
        }

        public ItemDurabilityOperationResult ExecuteSalvage(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            string sourceId = "",
            bool destroyIdentity = false)
        {
            if (!TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot snapshot))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.MissingDurability, $"Item durability for '{itemInstanceId}' was not found.");
            }

            ItemDurabilityRecordData record = snapshot.Data.Clone();
            List<ItemSalvageOutputData> outputs = BuildSalvageOutputs(record);
            record.salvageOutputs = outputs;
            record.salvageState = ItemSalvageState.Salvaged;
            record.functionalState = ItemFunctionalState.Destroyed;
            record.breakageState = ItemBreakageState.Destroyed;
            record.currentDurability = 0f;
            record.conditionBandId = "condition.destroyed";
            AddRevision(record, "durability.salvage", sourceId, "Item salvaged.");
            ItemDurabilityOperationResult set = SetDurabilityRecord(itemRuntime, compositionRuntime, qualityRuntime, registry, record);
            if (!set.Succeeded)
            {
                return set;
            }

            if (destroyIdentity && itemRuntime != null)
            {
                itemRuntime.DestroyOrConsume(itemInstanceId, consumed: false);
            }

            return ItemDurabilityOperationResult.Success(set.Snapshot, "Item salvaged.", salvageOutputs: outputs);
        }

        public ItemDurabilityProjection Project(string itemInstanceId, InformationAccessDecision decision = null)
        {
            if (!TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot snapshot))
            {
                return new ItemDurabilityProjection(null, denied: true, redacted: false, Array.Empty<ItemComponentDurabilityData>(), Array.Empty<string>());
            }

            if (decision == null || decision.Decision == InformationAccessDecisionKind.FullAccess)
            {
                return new ItemDurabilityProjection(snapshot, denied: false, redacted: false, snapshot.Components, Array.Empty<string>());
            }

            bool denied = decision.Decision == InformationAccessDecisionKind.Denied || decision.Decision == InformationAccessDecisionKind.MissingAuthorization;
            bool redacted = denied || decision.Decision == InformationAccessDecisionKind.RedactedAccess || decision.Decision == InformationAccessDecisionKind.PartialAccess;
            IReadOnlyList<ItemComponentDurabilityData> visible = redacted
                ? snapshot.Components.Where(component => !component.tags.Contains("hidden", StringComparer.Ordinal)).Select(RedactComponent).ToArray()
                : snapshot.Components;
            return new ItemDurabilityProjection(snapshot, denied, redacted, visible, decision.RedactedDetails.Concat(decision.HiddenDetails).ToArray());
        }

        public float GetEquipmentContributionFactor(string itemInstanceId)
        {
            if (!TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot snapshot))
            {
                return 1f;
            }

            ItemConditionBandData band = ResolveConditionBand(snapshot.NormalizedDurability);
            return band != null ? Mathf.Clamp01(band.equipmentContribution) : snapshot.FunctionalState >= ItemFunctionalState.Broken ? 0f : 1f;
        }

        public bool CanShareDurabilityStack(string leftItemInstanceId, string rightItemInstanceId)
        {
            if (!TryGetDurabilityForItem(leftItemInstanceId, out ItemDurabilitySnapshot left)
                || !TryGetDurabilityForItem(rightItemInstanceId, out ItemDurabilitySnapshot right))
            {
                return !TryGetDurabilityForItem(leftItemInstanceId, out _) && !TryGetDurabilityForItem(rightItemInstanceId, out _);
            }

            return left.ConditionBandId == right.ConditionBandId
                && left.FunctionalState == right.FunctionalState
                && Math.Abs(left.NormalizedDurability - right.NormalizedDurability) < 0.01f
                && Math.Abs(left.Data.permanentCapacityLoss - right.Data.permanentCapacityLoss) < 0.01f;
        }

        public ItemDurabilityRuntimeSaveData CreateSaveData()
        {
            return new ItemDurabilityRuntimeSaveData
            {
                schemaVersion = ItemDurabilityRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = recordsById.Values.OrderBy(record => record.itemInstanceId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public ItemDurabilityOperationResult RestoreFromSaveData(ItemDurabilityRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime = null)
        {
            ConfigureConditionScale(registry);
            if (!ValidateSaveData(saveData, registry, itemRuntime, compositionRuntime, out string failure))
            {
                return ItemDurabilityOperationResult.Failure(ItemDurabilityOperationStatus.RestoreFailed, failure);
            }

            recordsById.Clear();
            recordIdByItemId.Clear();
            foreach (ItemDurabilityRecordData record in saveData.records.Select(record => record.Clone()).OrderBy(record => record.itemInstanceId, StringComparer.Ordinal))
            {
                recordsById[record.durabilityRecordId] = record;
                recordIdByItemId[record.itemInstanceId] = record.durabilityRecordId;
            }

            revision = Math.Max(0L, saveData.revision);
            return ItemDurabilityOperationResult.Success(null, "Item durability runtime restored.");
        }

        public static bool ValidateSaveData(ItemDurabilityRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Item durability save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ItemDurabilityRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported item durability schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failure = "Item durability runtime revision cannot be negative.";
                return false;
            }

            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemDurabilityRecordData record in saveData.records ?? new List<ItemDurabilityRecordData>())
            {
                if (!ValidateRecord(record, registry, itemRuntime, compositionRuntime, out failure))
                {
                    return false;
                }

                if (!recordIds.Add(record.durabilityRecordId))
                {
                    failure = $"Duplicate durability record ID '{record.durabilityRecordId}'.";
                    return false;
                }

                if (!itemIds.Add(record.itemInstanceId))
                {
                    failure = $"Item instance '{record.itemInstanceId}' has more than one durability record.";
                    return false;
                }
            }

            return true;
        }

        private void NormalizeRecord(ItemDurabilityRecordData record)
        {
            if (record == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(record.durabilityRecordId) && !string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                record.durabilityRecordId = RecordId(record.itemInstanceId);
            }

            record.maximumDurability = Mathf.Max(1f, record.maximumDurability);
            record.originalMaximumDurability = Mathf.Max(record.maximumDurability, record.originalMaximumDurability);
            record.permanentCapacityLoss = Mathf.Clamp(record.permanentCapacityLoss, 0f, record.originalMaximumDurability - 1f);
            record.maximumDurability = Mathf.Max(1f, record.originalMaximumDurability - record.permanentCapacityLoss);
            record.currentDurability = Mathf.Clamp(record.currentDurability, 0f, record.maximumDurability);
            record.recoverableDamage = Mathf.Max(0f, record.maximumDurability - record.currentDurability);
            record.irrecoverableDamage = record.permanentCapacityLoss;
            record.components ??= new List<ItemComponentDurabilityData>();
            record.damageChannels ??= new List<ItemDamageChannelStateData>();
            record.repairHistory ??= new List<ItemRepairRecordData>();
            record.salvageOutputs ??= new List<ItemSalvageOutputData>();
            record.revisionHistory ??= new List<ItemDurabilityRevisionData>();

            foreach (ItemComponentDurabilityData component in record.components)
            {
                component.maximumDurability = Mathf.Max(1f, component.maximumDurability <= 0f ? record.maximumDurability : component.maximumDurability);
                component.originalMaximumDurability = Mathf.Max(component.maximumDurability, component.originalMaximumDurability <= 0f ? component.maximumDurability : component.originalMaximumDurability);
                component.permanentCapacityLoss = Mathf.Clamp(component.permanentCapacityLoss, 0f, component.originalMaximumDurability - 1f);
                component.maximumDurability = Mathf.Max(1f, component.originalMaximumDurability - component.permanentCapacityLoss);
                component.currentDurability = Mathf.Clamp(component.currentDurability <= 0f ? record.currentDurability : component.currentDurability, 0f, component.maximumDurability);
                EvaluateComponent(component);
            }
        }

        private void EvaluateRecord(ItemDurabilityRecordData record)
        {
            if (record == null)
            {
                return;
            }

            float normalized = record.maximumDurability <= 0f ? 0f : record.currentDurability / record.maximumDurability;
            ItemConditionBandData band = ResolveConditionBand(normalized);
            record.conditionBandId = band?.bandId ?? string.Empty;
            record.breakageState = band?.breakageState ?? (normalized <= 0f ? ItemBreakageState.Destroyed : ItemBreakageState.None);

            bool essentialBroken = record.components.Any(component => (component.criticality == ItemComponentCriticality.Critical || component.criticality == ItemComponentCriticality.Essential) && component.functionalState >= ItemFunctionalState.Broken);
            record.functionalState = record.breakageState == ItemBreakageState.Destroyed
                ? ItemFunctionalState.Destroyed
                : essentialBroken ? ItemFunctionalState.Broken
                : band?.functionalState ?? ItemFunctionalState.FullyFunctional;
            record.maintenanceState = record.wear > record.originalMaximumDurability * 0.5f
                ? ItemMaintenanceState.Overdue
                : record.wear > record.originalMaximumDurability * 0.25f ? ItemMaintenanceState.Due : ItemMaintenanceState.Maintained;
            record.salvageState = record.salvageState == ItemSalvageState.Salvaged
                ? ItemSalvageState.Salvaged
                : (band?.salvageEligible ?? false) || record.breakageState == ItemBreakageState.Broken
                    ? ItemSalvageState.Eligible
                    : ItemSalvageState.None;
        }

        private void EvaluateComponent(ItemComponentDurabilityData component)
        {
            float normalized = component.maximumDurability <= 0f ? 0f : component.currentDurability / component.maximumDurability;
            ItemConditionBandData band = ResolveConditionBand(normalized);
            component.breakageState = band?.breakageState ?? (normalized <= 0f ? ItemBreakageState.Destroyed : ItemBreakageState.None);
            component.functionalState = band?.functionalState ?? (normalized <= 0f ? ItemFunctionalState.Destroyed : ItemFunctionalState.FullyFunctional);
        }

        private void ConfigureConditionScale(DefinitionRegistry registry)
        {
            conditionScale = registry?.Defaults?.ItemConditionScale as ItemConditionScaleDefinition
                ?? registry?.DefinitionsById.Values.OfType<ItemConditionScaleDefinition>().OrderBy(scale => scale.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        private ItemConditionBandData ResolveConditionBand(float normalized)
        {
            return conditionScale != null && conditionScale.TryResolve(normalized, out ItemConditionBandData band) ? band : null;
        }

        private static bool ValidateRecord(ItemDurabilityRecordData record, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime, out string failure)
        {
            failure = string.Empty;
            if (record == null)
            {
                failure = "Durability record is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.durabilityRecordId))
            {
                failure = "Durability record is missing an ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.itemInstanceId) || !ItemInstanceId.IsValid(record.itemInstanceId))
            {
                failure = $"Durability record '{record.durabilityRecordId}' has invalid item instance ID '{record.itemInstanceId}'.";
                return false;
            }

            if (itemRuntime != null && !itemRuntime.TryGetSnapshot(record.itemInstanceId, out ItemInstanceSnapshot item))
            {
                failure = $"Durability record '{record.durabilityRecordId}' references missing item instance '{record.itemInstanceId}'.";
                return false;
            }

            if (itemRuntime != null && itemRuntime.TryGetSnapshot(record.itemInstanceId, out item)
                && !string.Equals(item.ItemDefinitionId, record.itemDefinitionId, StringComparison.Ordinal))
            {
                failure = $"Durability record '{record.durabilityRecordId}' item definition '{record.itemDefinitionId}' does not match item '{item.ItemDefinitionId}'.";
                return false;
            }

            if (registry != null && !string.IsNullOrWhiteSpace(record.itemDefinitionId) && !registry.TryGet(record.itemDefinitionId, out IInventoryItemDefinition _))
            {
                failure = $"Durability record '{record.durabilityRecordId}' references unknown item definition '{record.itemDefinitionId}'.";
                return false;
            }

            if (record.maximumDurability <= 0f || record.originalMaximumDurability <= 0f || record.currentDurability < 0f)
            {
                failure = $"Durability record '{record.durabilityRecordId}' has invalid durability values.";
                return false;
            }

            HashSet<string> componentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemComponentDurabilityData component in record.components ?? new List<ItemComponentDurabilityData>())
            {
                if (string.IsNullOrWhiteSpace(component.componentEntryId))
                {
                    failure = $"Durability record '{record.durabilityRecordId}' has a component durability entry with no component ID.";
                    return false;
                }

                if (!componentIds.Add(component.componentEntryId))
                {
                    failure = $"Durability record '{record.durabilityRecordId}' has duplicate component durability '{component.componentEntryId}'.";
                    return false;
                }

                if (compositionRuntime != null
                    && compositionRuntime.TryGetSnapshotForItem(record.itemInstanceId, out ItemCompositionSnapshot composition)
                    && composition.Components.Count > 0
                    && !composition.Components.Any(entry => string.Equals(entry.componentEntryId, component.componentEntryId, StringComparison.Ordinal)))
                {
                    failure = $"Durability component '{component.componentEntryId}' does not exist in item composition '{composition.CompositionId}'.";
                    return false;
                }
            }

            return true;
        }

        private static ItemComponentDurabilityData RedactComponent(ItemComponentDurabilityData component)
        {
            ItemComponentDurabilityData redacted = component.Clone();
            redacted.currentDurability = 0f;
            redacted.maximumDurability = 0f;
            redacted.originalMaximumDurability = 0f;
            return redacted;
        }

        private static float ResolveQuality(ItemInstanceSnapshot item, ItemQualityAffixRuntime qualityRuntime, DefinitionRegistry registry)
        {
            if (qualityRuntime != null && qualityRuntime.TryGetQualityForItem(item.ItemInstanceId, out ItemQualitySnapshot quality))
            {
                return Mathf.Clamp01(quality.OverallQuality);
            }

            return registry?.Defaults?.DefaultQualityNormalized ?? 0.5f;
        }

        private static float ResolveMaterialDurability(string itemInstanceId, ItemCompositionRuntime compositionRuntime, DefinitionRegistry registry)
        {
            if (compositionRuntime == null || !compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition))
            {
                return 0.5f;
            }

            DerivedItemMaterialProperties properties = compositionRuntime.ComputeDerivedProperties(composition, registry);
            return Mathf.Clamp01(properties.WeightedDurabilityPotential <= 0f ? 0.5f : properties.WeightedDurabilityPotential);
        }

        private static List<ItemSalvageOutputData> BuildSalvageOutputs(ItemDurabilityRecordData record)
        {
            List<ItemSalvageOutputData> outputs = new List<ItemSalvageOutputData>();
            float yield = Mathf.Clamp01(record.maximumDurability <= 0f ? 0f : record.currentDurability / record.maximumDurability);
            if (yield <= 0f)
            {
                yield = 0.1f;
            }

            if (record.components != null && record.components.Count > 0)
            {
                foreach (ItemComponentDurabilityData component in record.components.OrderBy(entry => entry.componentEntryId, StringComparer.Ordinal))
                {
                    outputs.Add(new ItemSalvageOutputData
                    {
                        outputId = $"salvage.{record.itemInstanceId}.{component.componentEntryId}",
                        itemDefinitionId = record.itemDefinitionId,
                        quantity = Mathf.Max(0.01f, yield),
                        unit = "component",
                        sourceComponentEntryId = component.componentEntryId
                    });
                }

                return outputs;
            }

            outputs.Add(new ItemSalvageOutputData
            {
                outputId = $"salvage.{record.itemInstanceId}.base",
                itemDefinitionId = record.itemDefinitionId,
                quantity = Mathf.Max(0.01f, yield),
                unit = "item"
            });
            return outputs;
        }

        private static float RepairPenalty(ItemRepairQuality quality, float amount)
        {
            return quality switch
            {
                ItemRepairQuality.Poor => amount * 0.2f,
                ItemRepairQuality.Adequate => amount * 0.1f,
                ItemRepairQuality.Good => amount * 0.04f,
                ItemRepairQuality.Excellent => amount * 0.01f,
                ItemRepairQuality.Masterwork => 0f,
                _ => amount * 0.12f
            };
        }

        private static void AddRevision(ItemDurabilityRecordData record, string operationId, string sourceId, string message)
        {
            record.revisionHistory ??= new List<ItemDurabilityRevisionData>();
            record.revisionHistory.Add(new ItemDurabilityRevisionData
            {
                revision = Math.Max(1L, record.revision),
                operationId = operationId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                message = message ?? string.Empty
            });
        }

        private static ItemDurabilityOperationStatus ToStatus(string failure)
        {
            if (failure != null && failure.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ItemDurabilityOperationStatus.MissingItem;
            }

            return ItemDurabilityOperationStatus.ValidationFailed;
        }

        private static string RecordId(string itemInstanceId)
        {
            return $"item-durability.{itemInstanceId}";
        }
    }
}
