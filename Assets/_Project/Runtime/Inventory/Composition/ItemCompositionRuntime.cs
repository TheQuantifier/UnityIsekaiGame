using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Composition
{
    public sealed class ItemCompositionRuntime
    {
        private readonly Dictionary<string, ItemCompositionRecordData> recordsByCompositionId = new Dictionary<string, ItemCompositionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> compositionByItemInstanceId = new Dictionary<string, string>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int Count => recordsByCompositionId.Count;

        public IReadOnlyList<ItemCompositionSnapshot> Snapshots => recordsByCompositionId.Values
            .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
            .Select(record => new ItemCompositionSnapshot(record))
            .ToArray();

        public ItemCompositionOperationResult SetComposition(
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            ItemCompositionRecordData composition,
            bool preview)
        {
            return SetComposition(itemRuntime, registry, composition, ItemCompositionMutationPurpose.RuntimeGameplay, preview);
        }

        public ItemCompositionOperationResult SetComposition(
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            ItemCompositionRecordData composition,
            ItemCompositionMutationPurpose purpose = ItemCompositionMutationPurpose.RuntimeGameplay,
            bool preview = false)
        {
            if (itemRuntime == null)
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingRuntime, "Item identity runtime is missing.");
            }

            ItemCompositionRecordData working = composition?.Clone();
            NormalizeCompositionIdentity(working);
            ItemCompositionRuntimeSaveData comparison = CreateSaveData();
            comparison.records = comparison.records
                .Where(record => record != null
                    && !string.Equals(record.compositionId, working?.compositionId, StringComparison.Ordinal)
                    && !string.Equals(record.itemInstanceId, working?.itemInstanceId, StringComparison.Ordinal))
                .ToList();
            if (!ValidateRecord(working, registry, itemRuntime, comparison, out string failure))
            {
                return ItemCompositionOperationResult.Failure(ToStatus(failure), failure);
            }

            if (preview)
            {
                return ItemCompositionOperationResult.Success(new ItemCompositionSnapshot(working), "Item composition preview prepared.", preview: true);
            }

            if (compositionByItemInstanceId.TryGetValue(working.itemInstanceId, out string existingCompositionId)
                && !string.Equals(existingCompositionId, working.compositionId, StringComparison.Ordinal))
            {
                recordsByCompositionId.Remove(existingCompositionId);
            }

            bool replacing = recordsByCompositionId.TryGetValue(working.compositionId, out ItemCompositionRecordData existing);
            working.revision = Math.Max(1L, replacing ? existing.revision + 1L : working.revision);
            working.lastMutationPurpose = purpose;
            working.revisionHistory ??= new List<ItemCompositionRevisionData>();
            working.revisionHistory.Add(new ItemCompositionRevisionData
            {
                revision = working.revision,
                operationId = replacing ? $"composition.replace.{purpose}" : $"composition.create.{purpose}",
                source = working.source ?? string.Empty,
                message = replacing ? "Composition record replaced." : "Composition record created."
            });
            recordsByCompositionId[working.compositionId] = working;
            compositionByItemInstanceId[working.itemInstanceId] = working.compositionId;
            revision++;
            return ItemCompositionOperationResult.Success(new ItemCompositionSnapshot(working), "Item composition set.");
        }

        public ItemCompositionOperationResult CreateUnknownCompositionForItem(
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            bool preview = false)
        {
            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            return SetComposition(
                itemRuntime,
                registry,
                new ItemCompositionRecordData
                {
                    compositionId = $"item-composition.{itemInstanceId}",
                    itemInstanceId = itemInstanceId,
                    sourceItemDefinitionId = item.ItemDefinitionId,
                    completeness = ItemCompositionCompleteness.Unknown,
                    source = "default.unknown",
                    tags = new[] { "item.composition", "composition.unknown" }
                },
                ItemCompositionMutationPurpose.DefaultInitialization,
                preview);
        }

        public ItemCompositionOperationResult EnsureCompositionForItem(
            ItemInstanceIdentityRuntime itemRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            bool preview = false)
        {
            if (TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot existing))
            {
                return ItemCompositionOperationResult.Success(existing, "Item composition already exists.", preview);
            }

            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            if (registry != null
                && registry.TryGet(item.ItemDefinitionId, out UnityIsekaiGame.Inventory.ItemDefinition definition)
                && definition.DefaultCompositionTemplate != null
                && !definition.DefaultCompositionTemplate.IsEmpty)
            {
                return SetComposition(itemRuntime, registry, definition.DefaultCompositionTemplate.Instantiate(itemInstanceId, item.ItemDefinitionId), ItemCompositionMutationPurpose.DefaultInitialization, preview);
            }

            return CreateUnknownCompositionForItem(itemRuntime, registry, itemInstanceId, preview);
        }

        public bool TryGetSnapshotForItem(string itemInstanceId, out ItemCompositionSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(itemInstanceId) || !compositionByItemInstanceId.TryGetValue(itemInstanceId, out string compositionId))
            {
                return false;
            }

            return TryGetSnapshot(compositionId, out snapshot);
        }

        public bool TryGetSnapshot(string compositionId, out ItemCompositionSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(compositionId) && recordsByCompositionId.TryGetValue(compositionId, out ItemCompositionRecordData record))
            {
                snapshot = new ItemCompositionSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool CanShareStack(string leftItemInstanceId, string rightItemInstanceId)
        {
            if (!TryGetSnapshotForItem(leftItemInstanceId, out ItemCompositionSnapshot left) ||
                !TryGetSnapshotForItem(rightItemInstanceId, out ItemCompositionSnapshot right))
            {
                return false;
            }

            return CompositionSignature(left.Data) == CompositionSignature(right.Data);
        }

        public ItemCompositionProjection Project(string itemInstanceId, InformationAccessDecision decision = null)
        {
            if (!TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot snapshot))
            {
                return new ItemCompositionProjection(null, denied: true, redacted: false, Array.Empty<ItemMaterialEntryData>(), Array.Empty<ItemComponentEntryData>(), Array.Empty<string>());
            }

            bool denied = decision != null && (decision.Decision == InformationAccessDecisionKind.Denied || decision.Decision == InformationAccessDecisionKind.MissingAuthorization);
            bool redacted = denied || decision != null && (decision.Decision == InformationAccessDecisionKind.RedactedAccess || decision.Decision == InformationAccessDecisionKind.PartialAccess);
            if (denied)
            {
                return new ItemCompositionProjection(null, denied: true, redacted: true, Array.Empty<ItemMaterialEntryData>(), Array.Empty<ItemComponentEntryData>(), ItemCompositionInformationSubject.ProtectedFields);
            }

            IReadOnlyList<ItemMaterialEntryData> materials = redacted
                ? snapshot.Materials.Where(entry => entry != null && !entry.hidden).Select(Redact).ToArray()
                : snapshot.Materials.Select(entry => entry.Clone()).ToArray();
            IReadOnlyList<ItemComponentEntryData> components = redacted
                ? snapshot.Components.Where(entry => entry != null && !entry.hidden).Select(Redact).ToArray()
                : snapshot.Components.Select(entry => entry.Clone()).ToArray();
            return new ItemCompositionProjection(redacted ? RedactedSnapshot(snapshot) : snapshot, denied: false, redacted, materials, components, redacted ? ItemCompositionInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public DerivedItemMaterialProperties ComputeDerivedProperties(ItemCompositionSnapshot snapshot, DefinitionRegistry registry)
        {
            DerivedItemMaterialProperties result = new DerivedItemMaterialProperties();
            if (snapshot == null)
            {
                result.Incomplete = true;
                return result;
            }

            result.MassAuthority = snapshot.Data.massAuthority;
            result.GameplayMassAuthoritative = snapshot.Data.massAuthority == ItemCompositionMassAuthority.CompositionAuthoritative
                && snapshot.Completeness == ItemCompositionCompleteness.Complete;
            float weight = 0f;
            foreach (ItemMaterialEntryData entry in snapshot.Materials.Where(entry => entry != null).OrderBy(entry => entry.entryId, StringComparer.Ordinal))
            {
                if (registry == null || !registry.TryGet(entry.materialDefinitionId, out MaterialDefinition material))
                {
                    result.Incomplete = true;
                    continue;
                }

                float mass = QuantityToMassKg(entry.quantity, material.PhysicalProperties);
                if (mass <= 0f)
                {
                    mass = 1f;
                }

                weight += mass;
                result.KnownMassKg += QuantityToMassKg(entry.quantity, material.PhysicalProperties);
                result.WeightedHardness += material.PhysicalProperties.hardness * mass;
                result.WeightedDurability += material.PhysicalProperties.durability * mass;
                result.WeightedFlexibility += material.PhysicalProperties.flexibility * mass;
                result.WeightedConductivity += material.PhysicalProperties.conductivity * mass;
                result.WeightedFlammability += material.PhysicalProperties.flammability * mass;
                result.MaterialCount++;
            }

            if (weight > 0f)
            {
                result.WeightedHardness /= weight;
                result.WeightedDurability /= weight;
                result.WeightedFlexibility /= weight;
                result.WeightedConductivity /= weight;
                result.WeightedFlammability /= weight;
            }

            result.Incomplete |= snapshot.Completeness != ItemCompositionCompleteness.Complete;
            return result;
        }

        public MaterialCompatibilityEvaluation EvaluateCompatibility(ItemMaterialEntryData source, ItemMaterialEntryData target, DefinitionRegistry registry)
        {
            MaterialCompatibilityRuleDefinition rule = registry?.DefinitionsById.Values
                .OfType<MaterialCompatibilityRuleDefinition>()
                .Where(candidate => candidate.Matches(source, target))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .FirstOrDefault();

            if (rule == null)
            {
                return new MaterialCompatibilityEvaluation { Outcome = MaterialCompatibilityOutcome.Neutral, Message = "No matching material compatibility rule." };
            }

            return new MaterialCompatibilityEvaluation
            {
                Outcome = rule.Outcome,
                RuleId = rule.Id,
                Priority = rule.Priority,
                DurabilityMultiplier = rule.DurabilityMultiplier,
                Message = rule.Message
            };
        }

        public IReadOnlyList<string> ExpandCompositeMaterial(string materialDefinitionId, DefinitionRegistry registry, int maximumDepth = 16)
        {
            List<string> expanded = new List<string>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            Visit(materialDefinitionId, 0);
            return expanded.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            void Visit(string id, int depth)
            {
                if (string.IsNullOrWhiteSpace(id) || registry == null || depth > maximumDepth || !registry.TryGet(id, out MaterialDefinition material))
                {
                    return;
                }

                if (!visiting.Add(id))
                {
                    return;
                }

                if (material.Constituents.Count == 0)
                {
                    expanded.Add(id);
                }
                else
                {
                    foreach (CompositeMaterialConstituentDefinition constituent in material.Constituents.OrderBy(constituent => constituent.MaterialId, StringComparer.Ordinal))
                    {
                        Visit(constituent.MaterialId, depth + 1);
                    }
                }

                visiting.Remove(id);
            }
        }

        public CompositeMaterialExpansionResult ExpandCompositeMaterialConstituents(string materialDefinitionId, DefinitionRegistry registry, int maximumDepth = 16, bool expandNested = true)
        {
            if (string.IsNullOrWhiteSpace(materialDefinitionId) || registry == null || !registry.TryGet(materialDefinitionId, out MaterialDefinition material))
            {
                return new CompositeMaterialExpansionResult { Succeeded = false, Message = $"Material '{materialDefinitionId}' was not found." };
            }

            Dictionary<string, float> ratios = new Dictionary<string, float>(StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            string failure = string.Empty;
            bool ok = Visit(material, 1f, 0);
            return ok
                ? new CompositeMaterialExpansionResult
                {
                    Succeeded = true,
                    Message = "Composite material expanded.",
                    Entries = ratios.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new CompositeMaterialExpansionEntry { MaterialDefinitionId = pair.Key, Ratio = pair.Value, Purity = 1f })
                        .ToArray()
                }
                : new CompositeMaterialExpansionResult { Succeeded = false, Message = failure };

            bool Visit(MaterialDefinition current, float multiplier, int depth)
            {
                if (current == null || string.IsNullOrWhiteSpace(current.Id))
                {
                    failure = "Composite material contains a missing constituent.";
                    return false;
                }

                if (depth > maximumDepth)
                {
                    failure = $"Composite material expansion exceeded depth {maximumDepth}.";
                    return false;
                }

                if (!visiting.Add(current.Id))
                {
                    failure = $"Composite material cycle detected at '{current.Id}'.";
                    return false;
                }

                if (!expandNested || current.Constituents.Count == 0)
                {
                    ratios[current.Id] = ratios.TryGetValue(current.Id, out float existing) ? existing + multiplier : multiplier;
                    visiting.Remove(current.Id);
                    return true;
                }

                foreach (CompositeMaterialConstituentDefinition constituent in current.Constituents.OrderBy(entry => entry.MaterialId, StringComparer.Ordinal))
                {
                    if (constituent == null || string.IsNullOrWhiteSpace(constituent.MaterialId) || !registry.TryGet(constituent.MaterialId, out MaterialDefinition child))
                    {
                        failure = $"Composite material '{current.Id}' references missing constituent '{constituent?.MaterialId}'.";
                        return false;
                    }

                    if (constituent.Ratio <= 0f)
                    {
                        failure = $"Composite material '{current.Id}' has invalid constituent ratio for '{constituent.MaterialId}'.";
                        return false;
                    }

                    if (!Visit(child, multiplier * constituent.Ratio, depth + 1))
                    {
                        return false;
                    }
                }

                visiting.Remove(current.Id);
                return true;
            }
        }

        public ItemCompositionRuntimeSaveData CreateSaveData()
        {
            return new ItemCompositionRuntimeSaveData
            {
                schemaVersion = ItemCompositionRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = recordsByCompositionId.Values.OrderBy(record => record.compositionId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public ItemCompositionOperationResult RestoreFromSaveData(ItemCompositionRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime)
        {
            if (!ValidateSaveData(saveData, registry, itemRuntime, out string failure))
            {
                return ItemCompositionOperationResult.Failure(ItemCompositionOperationStatus.RestoreFailed, failure);
            }

            Dictionary<string, ItemCompositionRecordData> restored = saveData.records
                .Select(record => record.Clone())
                .ToDictionary(record => record.compositionId, StringComparer.Ordinal);
            recordsByCompositionId.Clear();
            compositionByItemInstanceId.Clear();
            foreach (KeyValuePair<string, ItemCompositionRecordData> pair in restored.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                recordsByCompositionId.Add(pair.Key, pair.Value);
                compositionByItemInstanceId[pair.Value.itemInstanceId] = pair.Key;
            }

            revision = Math.Max(0L, saveData.revision);
            return ItemCompositionOperationResult.Success(null, "Item composition runtime restored.");
        }

        public static bool ValidateSaveData(ItemCompositionRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Item composition save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ItemCompositionRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported item composition schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failure = "Item composition runtime revision cannot be negative.";
                return false;
            }

            ItemCompositionRuntimeSaveData accumulated = new ItemCompositionRuntimeSaveData { schemaVersion = saveData.schemaVersion, revision = saveData.revision };
            foreach (ItemCompositionRecordData record in saveData.records ?? new List<ItemCompositionRecordData>())
            {
                if (!ValidateRecord(record, registry, itemRuntime, accumulated, out failure))
                {
                    return false;
                }

                accumulated.records.Add(record.Clone());
            }

            return true;
        }

        private static bool ValidateRecord(ItemCompositionRecordData record, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntimeSaveData existing, out string failure)
        {
            failure = string.Empty;
            if (record == null)
            {
                failure = "Item composition record is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                failure = "Item composition record is missing an item instance ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.compositionId))
            {
                failure = $"Item composition for '{record.itemInstanceId}' is missing a composition ID.";
                return false;
            }

            if (itemRuntime != null && !itemRuntime.TryGetSnapshot(record.itemInstanceId, out ItemInstanceSnapshot item))
            {
                failure = $"Item composition '{record.compositionId}' references missing item instance '{record.itemInstanceId}'.";
                return false;
            }

            if (itemRuntime != null && itemRuntime.TryGetSnapshot(record.itemInstanceId, out item)
                && !string.IsNullOrWhiteSpace(record.sourceItemDefinitionId)
                && !string.Equals(record.sourceItemDefinitionId, item.ItemDefinitionId, StringComparison.Ordinal))
            {
                failure = $"Item composition '{record.compositionId}' source definition '{record.sourceItemDefinitionId}' does not match item definition '{item.ItemDefinitionId}'.";
                return false;
            }

            if ((existing?.records ?? new List<ItemCompositionRecordData>()).Any(candidate => string.Equals(candidate.compositionId, record.compositionId, StringComparison.Ordinal)))
            {
                failure = $"Duplicate item composition ID '{record.compositionId}'.";
                return false;
            }

            if ((existing?.records ?? new List<ItemCompositionRecordData>()).Any(candidate => string.Equals(candidate.itemInstanceId, record.itemInstanceId, StringComparison.Ordinal)))
            {
                failure = $"Item instance '{record.itemInstanceId}' has more than one composition record.";
                return false;
            }

            HashSet<string> materialIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemMaterialEntryData material in record.materials ?? new List<ItemMaterialEntryData>())
            {
                if (!ValidateMaterialEntry(material, registry, materialIds, out failure))
                {
                    failure = $"Item composition '{record.compositionId}': {failure}";
                    return false;
                }
            }

            if (!ValidateMaterialProportions(record, out failure))
            {
                failure = $"Item composition '{record.compositionId}': {failure}";
                return false;
            }

            if (!ValidateComponents(record, itemRuntime, materialIds, existing, out failure))
            {
                failure = $"Item composition '{record.compositionId}': {failure}";
                return false;
            }

            long previousRevision = 0L;
            foreach (ItemCompositionRevisionData history in record.revisionHistory ?? new List<ItemCompositionRevisionData>())
            {
                if (history == null || history.revision <= 0L || history.revision > record.revision || history.revision < previousRevision)
                {
                    failure = $"Item composition '{record.compositionId}' has invalid revision history.";
                    return false;
                }

                previousRevision = history.revision;
            }

            return true;
        }

        private static bool ValidateMaterialEntry(ItemMaterialEntryData material, DefinitionRegistry registry, HashSet<string> materialEntryIds, out string failure)
        {
            failure = string.Empty;
            if (material == null)
            {
                failure = "Material entry is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(material.entryId) || !materialEntryIds.Add(material.entryId))
            {
                failure = $"Material entry ID '{material.entryId}' is missing or duplicated.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(material.materialDefinitionId) || registry != null && !registry.TryGet(material.materialDefinitionId, out MaterialDefinition _))
            {
                failure = $"Material entry '{material.entryId}' references missing material '{material.materialDefinitionId}'.";
                return false;
            }

            if (!Enum.IsDefined(typeof(MaterialEntryRole), material.role) || !ValidateQuantity(material.quantity))
            {
                failure = $"Material entry '{material.entryId}' has an invalid role or quantity (role={material.role}, value={material.quantity?.value.ToString("0.####") ?? "null"}, unit={material.quantity?.unit.ToString() ?? "null"}).";
                return false;
            }

            if (registry != null
                && registry.TryGet(material.materialDefinitionId, out MaterialDefinition materialDefinition)
                && IsVolume(material.quantity.unit)
                && materialDefinition.PhysicalProperties.densityKgPerLiter <= 0f)
            {
                failure = $"Material entry '{material.entryId}' uses volume but material '{material.materialDefinitionId}' has no density.";
                return false;
            }

            if (float.IsNaN(material.purity) || float.IsInfinity(material.purity) || material.purity < 0f || material.purity > 1f)
            {
                failure = $"Material entry '{material.entryId}' purity must be within 0..1.";
                return false;
            }

            return true;
        }

        private static bool ValidateMaterialProportions(ItemCompositionRecordData record, out string failure)
        {
            failure = string.Empty;
            float ratio = 0f;
            float percent = 0f;
            foreach (ItemMaterialEntryData material in record.materials ?? new List<ItemMaterialEntryData>())
            {
                if (material?.quantity?.unit == MaterialQuantityUnit.Ratio)
                {
                    ratio += material.quantity.value;
                }

                if (material?.quantity?.unit == MaterialQuantityUnit.Percent)
                {
                    percent += material.quantity.value;
                }
            }

            if (ratio > 1.0001f)
            {
                failure = $"Ratio material quantities total {ratio:0.####}, above 1.";
                return false;
            }

            if (percent > 100.0001f)
            {
                failure = $"Percent material quantities total {percent:0.####}, above 100.";
                return false;
            }

            return true;
        }

        private static bool ValidateComponents(ItemCompositionRecordData record, ItemInstanceIdentityRuntime itemRuntime, HashSet<string> materialEntryIds, ItemCompositionRuntimeSaveData existing, out string failure)
        {
            failure = string.Empty;
            HashSet<string> componentIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ItemComponentEntryData> components = new Dictionary<string, ItemComponentEntryData>(StringComparer.Ordinal);
            HashSet<string> trackedItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemComponentEntryData component in record.components ?? new List<ItemComponentEntryData>())
            {
                if (component == null || string.IsNullOrWhiteSpace(component.componentEntryId) || !componentIds.Add(component.componentEntryId))
                {
                    failure = $"Component entry ID '{component?.componentEntryId}' is missing or duplicated.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ItemComponentKind), component.kind) || component.count <= 0)
                {
                    failure = $"Component '{component.componentEntryId}' has an invalid kind or count.";
                    return false;
                }

                if (component.kind == ItemComponentKind.TrackedItemInstance)
                {
                    if (string.IsNullOrWhiteSpace(component.componentItemInstanceId) || string.Equals(component.componentItemInstanceId, record.itemInstanceId, StringComparison.Ordinal))
                    {
                        failure = $"Tracked component '{component.componentEntryId}' has an invalid component item instance ID.";
                        return false;
                    }

                    if (!trackedItemIds.Add(component.componentItemInstanceId))
                    {
                        failure = $"Tracked component item '{component.componentItemInstanceId}' is embedded more than once.";
                        return false;
                    }

                    if (itemRuntime != null && !itemRuntime.TryGetSnapshot(component.componentItemInstanceId, out ItemInstanceSnapshot componentItem))
                    {
                        failure = $"Tracked component '{component.componentEntryId}' references missing item instance '{component.componentItemInstanceId}'.";
                        return false;
                    }

                    if (itemRuntime != null && itemRuntime.TryGetSnapshot(component.componentItemInstanceId, out componentItem)
                        && (componentItem.LifecycleState == ItemLifecycleState.Destroyed || componentItem.LifecycleState == ItemLifecycleState.Consumed))
                    {
                        failure = $"Tracked component '{component.componentEntryId}' references {componentItem.LifecycleState} item '{component.componentItemInstanceId}'.";
                        return false;
                    }

                    if (itemRuntime != null && itemRuntime.TryGetSnapshot(component.componentItemInstanceId, out componentItem)
                        && !IsReservedForComponent(componentItem, record.itemInstanceId, component.componentEntryId))
                    {
                        failure = $"Tracked component '{component.componentEntryId}' item '{component.componentItemInstanceId}' is not reserved for parent item '{record.itemInstanceId}'.";
                        return false;
                    }

                    if ((existing?.records ?? new List<ItemCompositionRecordData>()).Any(existingRecord => existingRecord != null
                        && !string.Equals(existingRecord.itemInstanceId, record.itemInstanceId, StringComparison.Ordinal)
                        && (existingRecord.components ?? new List<ItemComponentEntryData>()).Any(existingComponent => existingComponent != null
                            && string.Equals(existingComponent.componentItemInstanceId, component.componentItemInstanceId, StringComparison.Ordinal))))
                    {
                        failure = $"Tracked component item '{component.componentItemInstanceId}' is already embedded in another composition.";
                        return false;
                    }

                    if (HasTrackedItemCycle(record.itemInstanceId, component.componentItemInstanceId, existing, out string itemChain))
                    {
                        failure = $"Tracked component item cycle detected: {itemChain}.";
                        return false;
                    }
                }

                foreach (string materialEntryId in component.materialEntryIds ?? Array.Empty<string>())
                {
                    if (!materialEntryIds.Contains(materialEntryId))
                    {
                        failure = $"Component '{component.componentEntryId}' references missing material entry '{materialEntryId}'.";
                        return false;
                    }
                }

                components.Add(component.componentEntryId, component);
            }

            foreach (ItemComponentEntryData component in components.Values)
            {
                if (!string.IsNullOrWhiteSpace(component.parentComponentEntryId) && !components.ContainsKey(component.parentComponentEntryId))
                {
                    failure = $"Component '{component.componentEntryId}' references missing parent component '{component.parentComponentEntryId}'.";
                    return false;
                }
            }

            foreach (string componentId in components.Keys)
            {
                if (HasComponentCycle(componentId, components, out string chain))
                {
                    failure = $"Component graph cycle detected: {chain}.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsReservedForComponent(ItemInstanceSnapshot componentItem, string parentItemInstanceId, string componentEntryId)
        {
            ItemLocationStateData location = componentItem?.Data?.location;
            if (location == null)
            {
                return false;
            }

            bool reserved = location.kind == ItemLocationKind.ProductionReserved || location.kind == ItemLocationKind.Reserved;
            return reserved
                && string.Equals(location.containerId, parentItemInstanceId, StringComparison.Ordinal)
                && string.Equals(location.transitId, componentEntryId, StringComparison.Ordinal);
        }

        private static bool HasTrackedItemCycle(string parentItemInstanceId, string childItemInstanceId, ItemCompositionRuntimeSaveData existing, out string chain)
        {
            Dictionary<string, ItemCompositionRecordData> byItem = (existing?.records ?? new List<ItemCompositionRecordData>())
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.itemInstanceId))
                .ToDictionary(record => record.itemInstanceId, StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            List<string> path = new List<string> { parentItemInstanceId };
            bool cycle = Visit(childItemInstanceId);
            chain = string.Join(" -> ", path);
            return cycle;

            bool Visit(string itemId)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return false;
                }

                path.Add(itemId);
                if (string.Equals(itemId, parentItemInstanceId, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!visiting.Add(itemId) || !byItem.TryGetValue(itemId, out ItemCompositionRecordData record))
                {
                    path.RemoveAt(path.Count - 1);
                    return false;
                }

                foreach (string next in (record.components ?? new List<ItemComponentEntryData>())
                    .Where(component => component != null && component.kind == ItemComponentKind.TrackedItemInstance)
                    .Select(component => component.componentItemInstanceId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.Ordinal))
                {
                    if (Visit(next))
                    {
                        return true;
                    }
                }

                path.RemoveAt(path.Count - 1);
                visiting.Remove(itemId);
                return false;
            }
        }

        private static bool HasComponentCycle(string rootId, Dictionary<string, ItemComponentEntryData> components, out string chain)
        {
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            List<string> path = new List<string>();
            bool cycle = Visit(rootId);
            chain = string.Join(" -> ", path);
            return cycle;

            bool Visit(string id)
            {
                if (string.IsNullOrWhiteSpace(id) || !components.TryGetValue(id, out ItemComponentEntryData component))
                {
                    return false;
                }

                if (!visiting.Add(id))
                {
                    path.Add(id);
                    return true;
                }

                path.Add(id);
                if (Visit(component.parentComponentEntryId))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
                visiting.Remove(id);
                return false;
            }
        }

        private static bool ValidateQuantity(MaterialQuantityData quantity)
        {
            return quantity != null
                && Enum.IsDefined(typeof(MaterialQuantityUnit), quantity.unit)
                && quantity.unit != MaterialQuantityUnit.Unknown
                && !float.IsNaN(quantity.value)
                && !float.IsInfinity(quantity.value)
                && quantity.value > 0f
                && (quantity.unit != MaterialQuantityUnit.Count || Math.Abs(quantity.value - MathF.Round(quantity.value)) < 0.0001f)
                && (quantity.unit != MaterialQuantityUnit.Ratio || quantity.value <= 1f)
                && (quantity.unit != MaterialQuantityUnit.Percent || quantity.value <= 100f);
        }

        private static float QuantityToMassKg(MaterialQuantityData quantity, MaterialPhysicalPropertySet properties)
        {
            if (quantity == null)
            {
                return 0f;
            }

            return quantity.unit switch
            {
                MaterialQuantityUnit.Kilogram => quantity.value,
                MaterialQuantityUnit.Gram => quantity.value / 1000f,
                MaterialQuantityUnit.Liter => quantity.value * (properties?.densityKgPerLiter ?? 0f),
                MaterialQuantityUnit.Milliliter => quantity.value * (properties?.densityKgPerLiter ?? 0f) / 1000f,
                _ => 0f
            };
        }

        private static bool IsVolume(MaterialQuantityUnit unit)
        {
            return unit == MaterialQuantityUnit.Liter || unit == MaterialQuantityUnit.Milliliter;
        }

        private static string CanonicalQuantity(MaterialQuantityData quantity)
        {
            if (quantity == null)
            {
                return "none";
            }

            return quantity.unit switch
            {
                MaterialQuantityUnit.Gram => $"mass:{quantity.value / 1000f:0.######}",
                MaterialQuantityUnit.Kilogram => $"mass:{quantity.value:0.######}",
                MaterialQuantityUnit.Milliliter => $"volume:{quantity.value / 1000f:0.######}",
                MaterialQuantityUnit.Liter => $"volume:{quantity.value:0.######}",
                MaterialQuantityUnit.Percent => $"ratio:{quantity.value / 100f:0.######}",
                MaterialQuantityUnit.Ratio => $"ratio:{quantity.value:0.######}",
                MaterialQuantityUnit.Count => $"count:{MathF.Round(quantity.value):0}",
                _ => $"unknown:{quantity.value:0.######}"
            };
        }

        private static void NormalizeCompositionIdentity(ItemCompositionRecordData record)
        {
            if (record == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(record.compositionId) && !string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                record.compositionId = $"item-composition.{record.itemInstanceId}";
            }

            record.materials ??= new List<ItemMaterialEntryData>();
            record.components ??= new List<ItemComponentEntryData>();
            record.tags = (record.tags ?? Array.Empty<string>()).Concat(new[] { "item.composition" }).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static ItemCompositionOperationStatus ToStatus(string failure)
        {
            string text = failure ?? string.Empty;
            if (text.Contains("missing item instance"))
            {
                return ItemCompositionOperationStatus.MissingItem;
            }

            if (text.Contains("missing material"))
            {
                return ItemCompositionOperationStatus.MissingMaterial;
            }

            if (text.Contains("quantity") || text.Contains("Ratio") || text.Contains("Percent") || text.Contains("density"))
            {
                return ItemCompositionOperationStatus.InvalidQuantity;
            }

            if (text.Contains("Duplicate") || text.Contains("duplicated"))
            {
                return ItemCompositionOperationStatus.DuplicateEntry;
            }

            if (text.Contains("cycle"))
            {
                return ItemCompositionOperationStatus.InvalidGraph;
            }

            if (text.Contains("reserved") || text.Contains("embedded in another composition"))
            {
                return ItemCompositionOperationStatus.InvalidComponentLocation;
            }

            return ItemCompositionOperationStatus.ValidationFailed;
        }

        private static string CompositionSignature(ItemCompositionRecordData record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            string materials = string.Join(";", (record.materials ?? new List<ItemMaterialEntryData>())
                .OrderBy(entry => entry.layerIndex)
                .ThenBy(entry => entry.componentEntryId, StringComparer.Ordinal)
                .ThenBy(entry => entry.role)
                .ThenBy(entry => entry.materialDefinitionId, StringComparer.Ordinal)
                .ThenBy(entry => entry.entryId, StringComparer.Ordinal)
                .Select(entry => $"{entry.entryId}:{entry.materialDefinitionId}:{entry.role}:{CanonicalQuantity(entry.quantity)}:{entry.purity:0.######}:{entry.processedForm}:{entry.layerIndex}:{entry.componentEntryId}:{entry.hidden}:{entry.accessPolicyId}:{string.Join(",", entry.tags ?? Array.Empty<string>())}"));
            string components = string.Join(";", (record.components ?? new List<ItemComponentEntryData>())
                .OrderBy(entry => entry.parentComponentEntryId, StringComparer.Ordinal)
                .ThenBy(entry => entry.componentEntryId, StringComparer.Ordinal)
                .Select(entry => $"{entry.componentEntryId}:{entry.parentComponentEntryId}:{entry.kind}:{entry.componentItemInstanceId}:{entry.componentItemDefinitionId}:{entry.count}:{entry.detachable}:{entry.replaceable}:{entry.optional}:{entry.hidden}:{entry.accessPolicyId}:{string.Join(",", entry.materialEntryIds ?? Array.Empty<string>())}:{string.Join(",", entry.tags ?? Array.Empty<string>())}"));
            return $"{record.sourceItemDefinitionId}|{record.templateVersionId}|{record.completeness}|{record.massAuthority}|{materials}|{components}|{string.Join(",", record.provenanceIds ?? Array.Empty<string>())}";
        }

        private static ItemCompositionSnapshot RedactedSnapshot(ItemCompositionSnapshot snapshot)
        {
            ItemCompositionRecordData data = snapshot.Data.Clone();
            data.accessPolicyId = string.Empty;
            data.provenanceIds = Array.Empty<string>();
            data.revisionHistory = new List<ItemCompositionRevisionData>();
            data.materials = data.materials.Where(entry => entry != null && !entry.hidden).Select(Redact).ToList();
            data.components = data.components.Where(entry => entry != null && !entry.hidden).Select(Redact).ToList();
            return new ItemCompositionSnapshot(data);
        }

        private static ItemMaterialEntryData Redact(ItemMaterialEntryData entry)
        {
            ItemMaterialEntryData clone = entry.Clone();
            clone.purity = 0f;
            clone.accessPolicyId = string.Empty;
            return clone;
        }

        private static ItemComponentEntryData Redact(ItemComponentEntryData entry)
        {
            ItemComponentEntryData clone = entry.Clone();
            clone.componentItemInstanceId = string.Empty;
            clone.accessPolicyId = string.Empty;
            return clone;
        }
    }
}
