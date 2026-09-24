using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Inventory.Disassembly
{
    public sealed class DisassemblyRuntime
    {
        private readonly Dictionary<string, DisassemblyOperationRecordData> operationsById = new Dictionary<string, DisassemblyOperationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, NaturalDecompositionRecordData> naturalDecompositionsByItemId = new Dictionary<string, NaturalDecompositionRecordData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public IReadOnlyList<DisassemblyOperationRecordData> Operations => operationsById.Values.OrderBy(entry => entry.operationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<NaturalDecompositionRecordData> NaturalDecompositions => naturalDecompositionsByItemId.Values.OrderBy(entry => entry.itemInstanceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();

        public bool TryGetOperation(string operationId, out DisassemblyOperationRecordData operation)
        {
            if (!string.IsNullOrWhiteSpace(operationId) && operationsById.TryGetValue(operationId, out DisassemblyOperationRecordData found))
            {
                operation = found.Clone();
                return true;
            }
            operation = null;
            return false;
        }

        public NaturalDecompositionRecordData ScheduleNaturalDecomposition(string itemInstanceId, string sourceWorldEntityId, string sceneKey, double currentWorldSeconds, float normalizedCondition, NaturalDecompositionSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(itemInstanceId) || double.IsNaN(currentWorldSeconds) || double.IsInfinity(currentWorldSeconds)) return null;
            NaturalDecompositionSettings policy = settings?.Clone() ?? new NaturalDecompositionSettings();
            policy.immediateThreshold = Mathf.Clamp01(policy.immediateThreshold);
            policy.fastThreshold = Mathf.Clamp(policy.fastThreshold, policy.immediateThreshold, 1f);
            policy.slowDurationSeconds = Math.Max(0d, policy.slowDurationSeconds);
            policy.fastDurationSeconds = Math.Max(0d, policy.fastDurationSeconds);
            float condition = Mathf.Clamp01(normalizedCondition);
            NaturalDecompositionRate rate = condition <= policy.immediateThreshold
                ? NaturalDecompositionRate.Immediate
                : condition <= policy.fastThreshold ? NaturalDecompositionRate.Fast : NaturalDecompositionRate.Slow;
            double duration = rate switch
            {
                NaturalDecompositionRate.Immediate => 0d,
                NaturalDecompositionRate.Fast => policy.fastDurationSeconds,
                _ => policy.slowDurationSeconds
            };

            if (naturalDecompositionsByItemId.TryGetValue(itemInstanceId, out NaturalDecompositionRecordData existing)
                && existing.state == NaturalDecompositionState.Scheduled)
            {
                bool changed = false;
                if (existing.rate != rate)
                {
                    double newDue = currentWorldSeconds + duration;
                    existing.dueAtWorldSeconds = rate > existing.rate ? Math.Max(currentWorldSeconds, Math.Min(existing.dueAtWorldSeconds, newDue)) : newDue;
                    existing.scheduledAtWorldSeconds = currentWorldSeconds;
                    existing.rate = rate;
                    changed = true;
                }
                if (!Mathf.Approximately(existing.lastKnownCondition, condition)) { existing.lastKnownCondition = condition; changed = true; }
                string entity = sourceWorldEntityId ?? existing.sourceWorldEntityId;
                string scene = sceneKey ?? existing.sceneKey;
                if (!string.Equals(existing.sourceWorldEntityId, entity, StringComparison.Ordinal)) { existing.sourceWorldEntityId = entity; changed = true; }
                if (!string.Equals(existing.sceneKey, scene, StringComparison.Ordinal)) { existing.sceneKey = scene; changed = true; }
                if (changed) { existing.revision++; revision++; }
                return existing.Clone();
            }

            NaturalDecompositionRecordData record = new NaturalDecompositionRecordData
            {
                scheduleId = $"natural-decomposition.{itemInstanceId}",
                itemInstanceId = itemInstanceId,
                sourceWorldEntityId = sourceWorldEntityId ?? string.Empty,
                sceneKey = sceneKey ?? string.Empty,
                operationId = DeterministicGuid($"nature:{itemInstanceId}:{currentWorldSeconds:0.###}"),
                rate = rate,
                state = NaturalDecompositionState.Scheduled,
                lastKnownCondition = condition,
                scheduledAtWorldSeconds = currentWorldSeconds,
                dueAtWorldSeconds = currentWorldSeconds + duration
            };
            naturalDecompositionsByItemId[itemInstanceId] = record;
            revision++;
            return record.Clone();
        }

        public bool IsNaturalDecompositionDue(string itemInstanceId, double currentWorldSeconds, out NaturalDecompositionRecordData record)
        {
            if (naturalDecompositionsByItemId.TryGetValue(itemInstanceId ?? string.Empty, out NaturalDecompositionRecordData found)
                && found.state == NaturalDecompositionState.Scheduled)
            {
                record = found.Clone();
                return currentWorldSeconds >= found.dueAtWorldSeconds;
            }
            record = null;
            return false;
        }

        public bool CompleteNaturalDecomposition(string itemInstanceId, double currentWorldSeconds)
        {
            if (!naturalDecompositionsByItemId.TryGetValue(itemInstanceId ?? string.Empty, out NaturalDecompositionRecordData record)) return false;
            record.state = NaturalDecompositionState.Completed;
            record.completedAtWorldSeconds = Math.Max(record.scheduledAtWorldSeconds, currentWorldSeconds);
            record.revision++;
            revision++;
            return true;
        }

        public bool CancelNaturalDecomposition(string itemInstanceId)
        {
            if (!naturalDecompositionsByItemId.TryGetValue(itemInstanceId ?? string.Empty, out NaturalDecompositionRecordData record)
                || record.state != NaturalDecompositionState.Scheduled) return false;
            record.state = NaturalDecompositionState.Cancelled;
            record.revision++;
            revision++;
            return true;
        }

        public DisassemblyResult RecordSalvagePickup(string operationId, string sourceId, string actorPersonId, string worldTime, ItemDefinition item, SalvagePickupCalculation calculation, int physicalCollectedQuantity, int totalCollectedQuantity)
        {
            if (string.IsNullOrWhiteSpace(operationId) || item == null || calculation == null || physicalCollectedQuantity <= 0 || totalCollectedQuantity < physicalCollectedQuantity)
                return DisassemblyResult.Failure(DisassemblyOperationStatus.InvalidRequest, "A salvage pickup record requires an operation, item, calculation, and valid physical/total collected quantities.");
            if (operationsById.TryGetValue(operationId, out DisassemblyOperationRecordData existing))
                return DisassemblyResult.Success(existing, "Salvage pickup was already recorded.", duplicate: true);
            bool bonusCollected = totalCollectedQuantity > physicalCollectedQuantity;
            DisassemblyOperationRecordData operation = new DisassemblyOperationRecordData
            {
                operationId = operationId, itemInstanceId = sourceId ?? string.Empty, itemDefinitionId = item.Id, actorPersonId = actorPersonId ?? string.Empty,
                worldTime = worldTime ?? string.Empty, deterministicSeed = sourceId ?? operationId, operationKind = ItemRecoveryOperationKind.Salvage,
                skillId = calculation.SkillId, skillGrade = calculation.SkillGrade, skillUsed = calculation.SkillUsed, itemLevel = 1,
                itemRarityRank = item.Rarity?.Rank ?? 0, itemQuality = 0f, itemCondition = 1f,
                expectedEfficiency = calculation.BonusChance, actualEfficiency = bonusCollected ? 1f : 0f,
                efficiencyTier = DisassemblyScalingCalculator.Tier(calculation.BonusChance), state = DisassemblyOperationState.Completed,
                status = DisassemblyOperationStatus.Succeeded,
                outcomes =
                {
                    new DisassemblyComponentOutcomeData
                    {
                        outcomeId = "salvage-pickup.1", sourceItemDefinitionId = item.Id, sourceQuantity = physicalCollectedQuantity,
                        itemRarityRank = item.Rarity?.Rank ?? 0, componentRarityRank = item.Rarity?.Rank ?? 0, componentCondition = 1f,
                        recoveryChance = calculation.BonusChance, recoveryRoll = calculation.BonusRoll, quantityEfficiency = 1f,
                        quantityMultiplier = calculation.QuantityMultiplier,
                        returnedQuantity = totalCollectedQuantity
                    }
                }
            };
            operationsById.Add(operationId, operation.Clone());
            revision++;
            int bonusQuantity = totalCollectedQuantity - physicalCollectedQuantity;
            return DisassemblyResult.Success(operation, bonusCollected ? $"Salvaging recovered {bonusQuantity} bonus materials." : "Salvage pickup recorded.");
        }

        public DisassemblyResult Preview(DisassemblyRequest request, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability)
        {
            DisassemblyRequest preview = request?.Clone() ?? new DisassemblyRequest();
            preview.preview = true;
            return Execute(preview, registry, items, compositions, quality, durability);
        }

        public DisassemblyResult Execute(DisassemblyRequest request, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability)
        {
            DisassemblyRequest working = request?.Clone() ?? new DisassemblyRequest();
            Normalize(working);
            if (operationsById.TryGetValue(working.operationId, out DisassemblyOperationRecordData existing))
                return existing.state == DisassemblyOperationState.Completed
                    ? DisassemblyResult.Success(existing, "Recovery operation was already completed.", duplicate: true)
                    : DisassemblyResult.Failure(DisassemblyOperationStatus.InvalidRequest, $"Recovery operation '{working.operationId}' already exists.", existing);

            if (!Validate(working, registry, items, compositions, durability, out ItemInstanceSnapshot item, out ItemCompositionSnapshot composition, out string failure))
                return DisassemblyResult.Failure(string.IsNullOrWhiteSpace(working.itemInstanceId) ? DisassemblyOperationStatus.InvalidRequest : DisassemblyOperationStatus.Ineligible, failure);

            DisassemblyOperationRecordData operation = BuildOperation(working, registry, item, composition, quality, durability);
            if (working.preview)
            {
                operation.state = DisassemblyOperationState.Prepared;
                operation.status = DisassemblyOperationStatus.Preview;
                return DisassemblyResult.Success(operation, $"{working.operationKind} preview prepared.", preview: true);
            }

            RollbackSnapshot rollback = RollbackSnapshot.Capture(items, compositions, quality, durability, this);
            operation.state = DisassemblyOperationState.Executing;
            try
            {
                foreach (DisassemblyComponentOutcomeData outcome in operation.outcomes.Where(entry => entry.returnedQuantity > 0))
                {
                    if (!registry.TryGet(outcome.sourceItemDefinitionId, out ItemDefinition outputDefinition))
                        return FailAndRollback(operation, $"Recovered component definition '{outcome.sourceItemDefinitionId}' is unavailable.", registry, items, compositions, quality, durability, rollback);

                    if (!working.materializeOutputIdentities)
                    {
                        outcome.outputItemInstanceIds = Array.Empty<string>();
                        continue;
                    }

                    List<string> outputIds = new List<string>();
                    int remaining = outcome.returnedQuantity;
                    int stackIndex = 0;
                    while (remaining > 0)
                    {
                        int quantity = Math.Min(remaining, outputDefinition.MaximumStackSize);
                        string outputId = DeterministicGuid($"{operation.operationId}:{outcome.outcomeId}:{stackIndex++}");
                        ItemInstanceClassification classification = outputDefinition.InstanceMode == ItemInstanceMode.AlwaysInstanced
                            ? ItemInstanceClassification.IndividuallyTracked
                            : ItemInstanceClassification.Fungible;
                        ItemInstanceOperationResult created = items.CreateItem(outputDefinition, classification, outputId, operation.actorPersonId, working.ownerPersonId, working.ownerPersonId, operation.operationId, stackQuantity: quantity);
                        if (!created.Succeeded)
                            return FailAndRollback(operation, created.Message, registry, items, compositions, quality, durability, rollback);
                        outputIds.Add(outputId);
                        remaining -= quantity;
                    }
                    outcome.outputItemInstanceIds = outputIds.ToArray();
                }

                ItemInstanceOperationResult consumed = items.MarkDisassembled(operation.itemInstanceId);
                if (!consumed.Succeeded)
                    return FailAndRollback(operation, consumed.Message, registry, items, compositions, quality, durability, rollback);

                ItemDurabilityOperationResult durabilityResult = durability?.MarkDestroyedByItemRecovery(items, compositions, quality, registry, operation.itemInstanceId, operation.operationId);
                if (durabilityResult != null && !durabilityResult.Succeeded)
                    return FailAndRollback(operation, durabilityResult.Message, registry, items, compositions, quality, durability, rollback);

                operation.state = DisassemblyOperationState.Completed;
                operation.status = DisassemblyOperationStatus.Succeeded;
                operation.revision = 1L;
                operationsById.Add(operation.operationId, operation.Clone());
                revision++;
                return DisassemblyResult.Success(operation, $"{operation.operationKind} completed at {operation.efficiencyTier} efficiency.");
            }
            catch (Exception ex)
            {
                return FailAndRollback(operation, ex.Message, registry, items, compositions, quality, durability, rollback);
            }
        }

        public DisassemblyRuntimeSaveData CreateSaveData() => new DisassemblyRuntimeSaveData
        {
            revision = revision,
            operations = operationsById.Values.OrderBy(entry => entry.operationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
            naturalDecompositions = naturalDecompositionsByItemId.Values.OrderBy(entry => entry.itemInstanceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList()
        };

        public DisassemblyResult RestoreFromSaveData(DisassemblyRuntimeSaveData saveData, DefinitionRegistry registry)
        {
            if (!ValidateSaveData(saveData, registry, out string failure)) return DisassemblyResult.Failure(DisassemblyOperationStatus.RestoreFailed, failure);
            operationsById.Clear();
            foreach (DisassemblyOperationRecordData operation in saveData.operations) operationsById[operation.operationId] = operation.Clone();
            naturalDecompositionsByItemId.Clear();
            foreach (NaturalDecompositionRecordData record in saveData.naturalDecompositions ?? new List<NaturalDecompositionRecordData>()) naturalDecompositionsByItemId[record.itemInstanceId] = record.Clone();
            revision = Math.Max(0L, saveData.revision);
            return DisassemblyResult.Success(null, "Item recovery runtime restored.");
        }

        public static bool ValidateSaveData(DisassemblyRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null || saveData.schemaVersion != DisassemblyRuntimeSaveData.CurrentSchemaVersion) { failure = "Item recovery save data is missing or unsupported."; return false; }
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DisassemblyOperationRecordData operation in saveData.operations ?? new List<DisassemblyOperationRecordData>())
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.operationId) || !ids.Add(operation.operationId)) { failure = "Item recovery operation IDs must be present and unique."; return false; }
                if (operation.skillGrade < 0 || operation.skillGrade > 7 || operation.itemLevel < 1) { failure = $"Item recovery operation '{operation.operationId}' has invalid scaling data."; return false; }
                foreach (DisassemblyComponentOutcomeData outcome in operation.outcomes ?? new List<DisassemblyComponentOutcomeData>())
                    if (outcome == null || string.IsNullOrWhiteSpace(outcome.outcomeId) || outcome.returnedQuantity < 0 || outcome.quantityMultiplier < 1 || outcome.recoveryChance < 0f || outcome.recoveryChance > 1f || (outcome.returnedQuantity > 0 && registry != null && !registry.TryGet(outcome.sourceItemDefinitionId, out ItemDefinition _)))
                    { failure = $"Item recovery operation '{operation.operationId}' has an invalid component outcome."; return false; }
            }
            HashSet<string> scheduledItems = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> scheduleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NaturalDecompositionRecordData record in saveData.naturalDecompositions ?? new List<NaturalDecompositionRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.scheduleId) || string.IsNullOrWhiteSpace(record.itemInstanceId)
                    || !scheduleIds.Add(record.scheduleId) || !scheduledItems.Add(record.itemInstanceId)
                    || !Enum.IsDefined(typeof(NaturalDecompositionRate), record.rate) || !Enum.IsDefined(typeof(NaturalDecompositionState), record.state)
                    || record.lastKnownCondition < 0f || record.lastKnownCondition > 1f
                    || double.IsNaN(record.scheduledAtWorldSeconds) || double.IsInfinity(record.scheduledAtWorldSeconds)
                    || double.IsNaN(record.dueAtWorldSeconds) || double.IsInfinity(record.dueAtWorldSeconds)
                    || record.dueAtWorldSeconds < record.scheduledAtWorldSeconds)
                { failure = "Natural decomposition schedules must have unique IDs, valid item references, condition, state, and time bounds."; return false; }
            }
            return true;
        }

        private static DisassemblyOperationRecordData BuildOperation(DisassemblyRequest request, DefinitionRegistry registry, ItemInstanceSnapshot item, ItemCompositionSnapshot composition, ItemQualityAffixRuntime qualityRuntime, ItemDurabilityRuntime durabilityRuntime)
        {
            float quality = qualityRuntime != null && qualityRuntime.TryGetQualityForItem(item.ItemInstanceId, out ItemQualitySnapshot qualitySnapshot) ? Mathf.Clamp01(qualitySnapshot.OverallQuality) : 0.5f;
            string rarityId = qualityRuntime != null && qualityRuntime.TryGetQualityForItem(item.ItemInstanceId, out qualitySnapshot) ? qualitySnapshot.Data.rarity?.EffectiveRarityId : string.Empty;
            int itemRarity = !string.IsNullOrWhiteSpace(rarityId) && registry.TryGet(rarityId, out RarityDefinition effectiveRarity) ? effectiveRarity.Rank : (registry.TryGet(item.ItemDefinitionId, out ItemDefinition definition) ? definition.Rarity?.Rank ?? 0 : 0);
            ItemDurabilitySnapshot durability = null;
            float condition = durabilityRuntime != null && durabilityRuntime.TryGetDurabilityForItem(item.ItemInstanceId, out durability) ? Mathf.Clamp01(durability.NormalizedDurability) : 1f;
            int itemLevel = request.itemLevel > 0 ? request.itemLevel : Math.Max(1, 1 + itemRarity * 10 + Mathf.RoundToInt(quality * 10f));
            float expected = DisassemblyScalingCalculator.ExpectedEfficiency(request.skillGrade, request.skillUsed, itemLevel, itemRarity, quality, condition);
            DisassemblyOperationRecordData operation = new DisassemblyOperationRecordData
            {
                operationId = request.operationId, itemInstanceId = item.ItemInstanceId, itemDefinitionId = item.ItemDefinitionId, actorPersonId = request.actorPersonId,
                worldTime = request.worldTime, deterministicSeed = request.deterministicSeed, operationKind = request.operationKind, skillId = request.skillId,
                skillGrade = request.skillGrade, skillUsed = request.skillUsed, itemLevel = itemLevel, itemRarityRank = itemRarity,
                itemQuality = quality, itemCondition = condition, expectedEfficiency = expected, state = DisassemblyOperationState.Prepared, status = DisassemblyOperationStatus.Preview
            };

            int index = 0;
            foreach (ItemMaterialEntryData material in composition.Materials.OrderBy(entry => entry.componentEntryId, StringComparer.Ordinal).ThenBy(entry => entry.entryId, StringComparer.Ordinal))
            {
                string sourceDefinitionId = ResolveSourceDefinitionId(material, registry);
                if (string.IsNullOrWhiteSpace(sourceDefinitionId)) continue;
                registry.TryGet(sourceDefinitionId, out ItemDefinition componentDefinition);
                int componentRarity = componentDefinition?.Rarity?.Rank ?? 0;
                ItemComponentDurabilityData componentDurability = durability?.Components.FirstOrDefault(entry => string.Equals(entry.componentEntryId, material.componentEntryId, StringComparison.Ordinal));
                float componentCondition = componentDurability == null || componentDurability.maximumDurability <= 0f
                    ? condition
                    : Mathf.Clamp01(componentDurability.currentDurability / componentDurability.maximumDurability);
                string seed = $"{request.deterministicSeed}:{material.componentEntryId}:{material.entryId}:{index}";
                float chance = DisassemblyScalingCalculator.ComponentRecoveryChance(expected, componentRarity, componentCondition);
                float efficiency = DisassemblyScalingCalculator.QuantityEfficiency(expected, componentRarity, componentCondition, seed);
                float quantity = Math.Max(0f, material.quantity?.value ?? 0f);
                operation.outcomes.Add(new DisassemblyComponentOutcomeData
                {
                    outcomeId = $"recovery.{++index}", componentEntryId = material.componentEntryId, materialEntryId = material.entryId,
                    sourceItemDefinitionId = sourceDefinitionId, materialDefinitionId = material.materialDefinitionId, sourceQuantity = quantity,
                    itemRarityRank = itemRarity, componentRarityRank = componentRarity, recoveryChance = chance,
                    componentCondition = componentCondition,
                    recoveryRoll = DisassemblyScalingCalculator.DeterministicUnitInterval(seed + ":chance"), quantityEfficiency = efficiency,
                    quantityMultiplier = 1,
                    returnedQuantity = DisassemblyScalingCalculator.RecoveredQuantity(quantity, chance, efficiency, seed)
                });
            }

            float sourceTotal = operation.outcomes.Sum(entry => entry.sourceQuantity);
            operation.actualEfficiency = sourceTotal <= 0f ? 0f : Mathf.Clamp01(operation.outcomes.Sum(entry => entry.returnedQuantity) / sourceTotal);
            operation.efficiencyTier = DisassemblyScalingCalculator.Tier(operation.actualEfficiency);
            return operation;
        }

        private static string ResolveSourceDefinitionId(ItemMaterialEntryData material, DefinitionRegistry registry)
        {
            if (!string.IsNullOrWhiteSpace(material.sourceItemDefinitionId) && registry.TryGet(material.sourceItemDefinitionId, out ItemDefinition _)) return material.sourceItemDefinitionId;
            return registry.DefinitionsById.Values.OfType<ItemDefinition>()
                .Where(item => item.InstanceMode == ItemInstanceMode.DefinitionOnly && item.DefaultCompositionTemplate.materials.Any(entry => string.Equals(entry.materialDefinitionId, material.materialDefinitionId, StringComparison.Ordinal)))
                .OrderBy(item => item.Rarity?.Rank ?? 0).ThenBy(item => item.Id, StringComparer.Ordinal).Select(item => item.Id).FirstOrDefault() ?? string.Empty;
        }

        private static bool Validate(DisassemblyRequest request, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemDurabilityRuntime durability, out ItemInstanceSnapshot item, out ItemCompositionSnapshot composition, out string failure)
        {
            item = null; composition = null; failure = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.operationId) || string.IsNullOrWhiteSpace(request.itemInstanceId) || registry == null || items == null || compositions == null) { failure = "Item recovery requires an operation ID, item, registry, identity runtime, and composition runtime."; return false; }
            if (!items.TryGetSnapshot(request.itemInstanceId, out item)) { failure = $"Item '{request.itemInstanceId}' was not found."; return false; }
            if (item.LifecycleState is ItemLifecycleState.Disassembled or ItemLifecycleState.Consumed or ItemLifecycleState.Destroyed) { failure = "The item is already in a terminal state."; return false; }
            if (!compositions.TryGetSnapshotForItem(request.itemInstanceId, out composition) || composition.Materials.Count == 0) { failure = "The item has no recoverable composition."; return false; }
            bool crafted = composition.Data.tags?.Contains("composition.crafted", StringComparer.Ordinal) == true || composition.Materials.Any(entry => !string.IsNullOrWhiteSpace(entry.sourceItemDefinitionId));
            if (!crafted && request.operationKind != ItemRecoveryOperationKind.NaturalDecomposition) { failure = "Intentional disassembly requires recorded crafted composition."; return false; }
            if (request.operationKind == ItemRecoveryOperationKind.Salvage) { failure = "Salvage is a world-pickup operation; crafted items use Disassemble or NaturalDecomposition."; return false; }
            return true;
        }

        private static void Normalize(DisassemblyRequest request)
        {
            request.operationId = request.operationId?.Trim() ?? string.Empty;
            request.itemInstanceId = request.itemInstanceId?.Trim() ?? string.Empty;
            request.actorPersonId = request.actorPersonId?.Trim() ?? string.Empty;
            request.ownerPersonId = string.IsNullOrWhiteSpace(request.ownerPersonId) ? request.actorPersonId : request.ownerPersonId.Trim();
            request.deterministicSeed = string.IsNullOrWhiteSpace(request.deterministicSeed) ? request.operationId : request.deterministicSeed;
            request.skillGrade = Mathf.Clamp(request.skillGrade, 0, 7);
        }

        private DisassemblyResult FailAndRollback(DisassemblyOperationRecordData operation, string failure, DefinitionRegistry registry, ItemInstanceIdentityRuntime items, ItemCompositionRuntime compositions, ItemQualityAffixRuntime quality, ItemDurabilityRuntime durability, RollbackSnapshot rollback)
        {
            operation.state = DisassemblyOperationState.Failed; operation.status = DisassemblyOperationStatus.AtomicCommitFailed; operation.diagnostics = new[] { failure ?? "Item recovery failed." };
            rollback.Restore(registry, items, compositions, quality, durability, this);
            return DisassemblyResult.Failure(DisassemblyOperationStatus.AtomicCommitFailed, failure, operation);
        }

        private static string DeterministicGuid(string seed)
        {
            using MD5 md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty))).ToString("D");
        }

        private sealed class RollbackSnapshot
        {
            private ItemInstanceRuntimeSaveData items; private ItemCompositionRuntimeSaveData compositions; private ItemQualityAffixRuntimeSaveData quality; private ItemDurabilityRuntimeSaveData durability; private DisassemblyRuntimeSaveData disassembly;
            public static RollbackSnapshot Capture(ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime, ItemQualityAffixRuntime qualityRuntime, ItemDurabilityRuntime durabilityRuntime, DisassemblyRuntime runtime) => new RollbackSnapshot
            { items = itemRuntime?.CreateSaveData(), compositions = compositionRuntime?.CreateSaveData(), quality = qualityRuntime?.CreateSaveData(), durability = durabilityRuntime?.CreateSaveData(), disassembly = runtime?.CreateSaveData() };
            public void Restore(DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime, ItemQualityAffixRuntime qualityRuntime, ItemDurabilityRuntime durabilityRuntime, DisassemblyRuntime runtime)
            {
                if (items != null) itemRuntime?.RestoreFromSaveData(items, registry);
                if (compositions != null) compositionRuntime?.RestoreFromSaveData(compositions, registry, itemRuntime);
                if (quality != null) qualityRuntime?.RestoreFromSaveData(quality, registry, itemRuntime);
                if (durability != null) durabilityRuntime?.RestoreFromSaveData(durability, registry, itemRuntime, compositionRuntime);
                if (disassembly != null) runtime?.RestoreFromSaveData(disassembly, registry);
            }
        }
    }
}
