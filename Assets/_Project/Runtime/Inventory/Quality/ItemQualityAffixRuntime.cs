using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Stats;
using static UnityIsekaiGame.Inventory.Quality.ItemQualityAffixCloneUtility;

namespace UnityIsekaiGame.Inventory.Quality
{
    public sealed class ItemQualityAffixRuntime
    {
        private readonly Dictionary<string, ItemQualityRecordData> qualityById = new Dictionary<string, ItemQualityRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> qualityIdByItemId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemAffixInstanceData> affixesById = new Dictionary<string, ItemAffixInstanceData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int QualityRecordCount => qualityById.Count;
        public int AffixCount => affixesById.Count;
        public event Action<string> ItemAffixStateChanged;

        public IReadOnlyList<ItemQualitySnapshot> QualitySnapshots => qualityById.Values
            .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
            .Select(record => new ItemQualitySnapshot(record))
            .ToArray();

        public IReadOnlyList<ItemAffixSnapshot> AffixSnapshots => affixesById.Values
            .OrderBy(record => record.itemInstanceId, StringComparer.Ordinal)
            .ThenBy(record => record.affixInstanceId, StringComparer.Ordinal)
            .Select(record => new ItemAffixSnapshot(record))
            .ToArray();

        public bool TryGetQualityForItem(string itemInstanceId, out ItemQualitySnapshot snapshot)
        {
            snapshot = null;
            return !string.IsNullOrWhiteSpace(itemInstanceId)
                && qualityIdByItemId.TryGetValue(itemInstanceId, out string qualityRecordId)
                && TryGetQuality(qualityRecordId, out snapshot);
        }

        public bool TryGetQuality(string qualityRecordId, out ItemQualitySnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(qualityRecordId) && qualityById.TryGetValue(qualityRecordId, out ItemQualityRecordData record))
            {
                snapshot = new ItemQualitySnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<ItemAffixSnapshot> GetAffixesForItem(string itemInstanceId, bool activeOnly = false)
        {
            return affixesById.Values
                .Where(record => string.Equals(record.itemInstanceId, itemInstanceId, StringComparison.Ordinal) && !record.removed && (!activeOnly || record.active))
                .OrderBy(record => record.classification)
                .ThenBy(record => record.affixDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => record.affixTierId, StringComparer.Ordinal)
                .ThenBy(record => record.affixInstanceId, StringComparer.Ordinal)
                .Select(record => new ItemAffixSnapshot(record))
                .ToArray();
        }

        public IReadOnlyList<ItemAffixSnapshot> GetAffixHistoryForItem(string itemInstanceId)
        {
            return affixesById.Values
                .Where(record => string.Equals(record.itemInstanceId, itemInstanceId, StringComparison.Ordinal))
                .OrderBy(record => record.classification)
                .ThenBy(record => record.affixDefinitionId, StringComparer.Ordinal)
                .ThenBy(record => record.affixTierId, StringComparer.Ordinal)
                .ThenBy(record => record.affixInstanceId, StringComparer.Ordinal)
                .Select(record => new ItemAffixSnapshot(record))
                .ToArray();
        }

        public ItemQualityAffixOperationResult EnsureDefaultQuality(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            bool preview = false)
        {
            if (TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot existing))
            {
                return ItemQualityAffixOperationResult.Success(existing, "Item quality already exists.", preview);
            }

            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingItem, $"Item instance '{itemInstanceId}' was not found.");
            }

            float inherited = registry?.Defaults?.DefaultQualityNormalized ?? 0.5f;

            ItemQualityRecordData record = new ItemQualityRecordData
            {
                qualityRecordId = QualityRecordId(itemInstanceId),
                itemInstanceId = itemInstanceId,
                itemDefinitionId = item.ItemDefinitionId,
                overallQuality = inherited,
                source = ItemQualityRecordSource.DefinitionDefault,
                generationPolicyId = "quality-policy.default",
                relatedCompositionRevision = compositionRuntime != null && compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition) ? composition.Revision : 0L,
                workmanship =
                {
                    Workmanship("workmanship.overall", WorkmanshipDimension.Overall, inherited, QualityValueState.Known)
                },
                dimensions =
                {
                    Dimension("quality.workmanship", ItemQualityDimension.Workmanship, inherited, QualityValueState.Known, 1f)
                },
                tags = new[] { "item.quality", "quality.default" }
            };
            ApplyEvaluation(record, registry, compositionRuntime);
            return SetQualityRecord(itemRuntime, compositionRuntime, registry, record, preview);
        }

        public ItemQualityAffixOperationResult SetQualityRecord(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            ItemQualityRecordData record,
            bool preview = false)
        {
            if (itemRuntime == null)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingRuntime, "Item identity runtime is missing.");
            }

            ItemQualityRecordData working = record?.Clone();
            NormalizeQuality(working);
            ApplyEvaluation(working, registry, compositionRuntime);
            if (!ValidateQualityRecord(working, registry, itemRuntime, compositionRuntime, out string failure))
            {
                return ItemQualityAffixOperationResult.Failure(ToStatus(failure), failure);
            }

            if (preview)
            {
                return ItemQualityAffixOperationResult.Success(new ItemQualitySnapshot(working), "Item quality preview prepared.", true);
            }

            bool replacing = qualityById.TryGetValue(working.qualityRecordId, out ItemQualityRecordData existing);
            if (qualityIdByItemId.TryGetValue(working.itemInstanceId, out string existingForItem)
                && !string.Equals(existingForItem, working.qualityRecordId, StringComparison.Ordinal))
            {
                qualityById.Remove(existingForItem);
            }

            working.revision = Math.Max(1L, replacing ? existing.revision + 1L : working.revision);
            working.revisionHistory ??= new List<ItemQualityRevisionData>();
            working.revisionHistory.Add(new ItemQualityRevisionData
            {
                revision = working.revision,
                operationId = replacing ? "quality.replace" : "quality.create",
                sourceId = working.source.ToString(),
                message = replacing ? "Quality record replaced." : "Quality record created."
            });
            qualityById[working.qualityRecordId] = working;
            qualityIdByItemId[working.itemInstanceId] = working.qualityRecordId;
            revision++;
            return ItemQualityAffixOperationResult.Success(new ItemQualitySnapshot(working), "Item quality set.");
        }

        public ItemQualityEvaluationResult EvaluateQuality(
            string itemInstanceId,
            DefinitionRegistry registry,
            ItemCompositionRuntime compositionRuntime)
        {
            if (!TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot snapshot))
            {
                return new ItemQualityEvaluationResult { Succeeded = false, Diagnostics = new[] { "Quality record is missing." } };
            }

            ItemQualityRecordData working = snapshot.Data.Clone();
            ApplyEvaluation(working, registry, compositionRuntime);
            return new ItemQualityEvaluationResult
            {
                Succeeded = true,
                OverallQuality = working.overallQuality,
                QualityTierId = working.qualityTierId,
                DerivedRarityId = working.rarity?.derivedRarityId ?? string.Empty,
                RarityScore = working.rarity?.derivedScore ?? 0f,
                ContributingInputs = working.dimensions.Select(entry => entry.entryId).Concat(working.workmanship.Select(entry => entry.entryId)).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                Diagnostics = new[] { $"Quality={working.overallQuality:0.###}", $"Tier={working.qualityTierId}", $"Rarity={working.rarity?.EffectiveRarityId}" }
            };
        }

        public ItemQualityAffixOperationResult AddDefect(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            ItemDefectEntryData defect,
            bool preview = false)
        {
            ItemQualityAffixOperationResult ensured = EnsureDefaultQuality(itemRuntime, compositionRuntime, registry, itemInstanceId, preview: false);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            ItemQualityRecordData record = ensured.Quality.Data.Clone();
            ItemDefectEntryData clone = defect?.Clone();
            if (clone == null || string.IsNullOrWhiteSpace(clone.defectId))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.InvalidRequest, "Defect requires a stable defect ID.");
            }

            if (record.defects.Any(entry => string.Equals(entry.defectId, clone.defectId, StringComparison.Ordinal)))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.DuplicateRecord, $"Defect '{clone.defectId}' already exists.");
            }

            record.defects.Add(clone);
            return SetQualityRecord(itemRuntime, compositionRuntime, registry, record, preview);
        }

        public ItemAffixEligibilityResult EvaluateAffixEligibility(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            ItemAffixDefinition definition)
        {
            List<string> reasons = new List<string>();
            List<string> failed = new List<string>();
            List<string> conflicts = new List<string>();
            List<string> tiers = new List<string>();

            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                failed.Add("missing-item");
                return Eligibility(false, reasons, failed, tiers, conflicts);
            }

            if (definition == null)
            {
                failed.Add("missing-affix-definition");
                return Eligibility(false, reasons, failed, tiers, conflicts);
            }

            if (!TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality))
            {
                failed.Add("missing-quality");
            }

            float qualityValue = quality?.OverallQuality ?? -1f;
            tiers.AddRange(definition.Tiers.Where(tier => tier != null && qualityValue >= tier.minimumItemQuality && qualityValue <= tier.maximumItemQuality).Select(tier => tier.tierId).OrderBy(id => id, StringComparer.Ordinal));
            if (tiers.Count == 0)
            {
                failed.Add("quality-tier");
            }

            if (definition.ApplicableItemDefinitions.Count > 0 && !definition.ApplicableItemDefinitions.Any(candidate => candidate != null && string.Equals(candidate.Id, item.ItemDefinitionId, StringComparison.Ordinal)))
            {
                failed.Add("item-definition");
            }

            if (definition.ApplicableCategories.Count > 0)
            {
                string itemCategory = registry != null && registry.TryGet(item.ItemDefinitionId, out ItemDefinition itemDefinition) ? itemDefinition.PrimaryCategory?.Id : string.Empty;
                if (string.IsNullOrWhiteSpace(itemCategory) || !definition.ApplicableCategories.Any(category => category != null && string.Equals(category.Id, itemCategory, StringComparison.Ordinal)))
                {
                    failed.Add("item-category");
                }
            }

            HashSet<string> itemTags = registry != null && registry.TryGet(item.ItemDefinitionId, out ItemDefinition fullDefinition)
                ? new HashSet<string>(fullDefinition.Tags.Where(tag => tag != null).Select(tag => tag.Id), StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            if (definition.RequiredItemTags.Any(tag => tag != null && !itemTags.Contains(tag.Id)))
            {
                failed.Add("required-item-tag");
            }

            if (definition.ForbiddenItemTags.Any(tag => tag != null && itemTags.Contains(tag.Id)))
            {
                failed.Add("forbidden-item-tag");
            }

            HashSet<string> materialTags = MaterialTags(compositionRuntime, registry, itemInstanceId);
            if (definition.RequiredMaterialTags.Any(tag => tag != null && !materialTags.Contains(tag.Id)))
            {
                failed.Add("required-material-tag");
            }

            if (definition.ForbiddenMaterialTags.Any(tag => tag != null && materialTags.Contains(tag.Id)))
            {
                failed.Add("forbidden-material-tag");
            }

            IReadOnlyList<ItemAffixSnapshot> existing = GetAffixesForItem(itemInstanceId, activeOnly: true);
            int sameDefinition = existing.Count(affix => string.Equals(affix.AffixDefinitionId, definition.Id, StringComparison.Ordinal));
            if (sameDefinition >= definition.MaximumOccurrences)
            {
                failed.Add("maximum-occurrences");
            }

            if (definition.Classification == ItemAffixClassification.Prefix && existing.Count(affix => affix.Data.classification == ItemAffixClassification.Prefix) >= definition.MaximumPrefixCount)
            {
                failed.Add("maximum-prefix-count");
            }

            if (definition.Classification == ItemAffixClassification.Suffix && existing.Count(affix => affix.Data.classification == ItemAffixClassification.Suffix) >= definition.MaximumSuffixCount)
            {
                failed.Add("maximum-suffix-count");
            }

            if (existing.Count >= definition.MaximumTotalAffixCount)
            {
                failed.Add("maximum-total-count");
            }

            HashSet<string> exclusiveGroups = new HashSet<string>(definition.ExclusiveGroups.Where(group => !string.IsNullOrWhiteSpace(group)), StringComparer.Ordinal);
            foreach (ItemAffixSnapshot affix in existing)
            {
                if (registry != null && registry.TryGet(affix.AffixDefinitionId, out ItemAffixDefinition existingDefinition)
                    && existingDefinition.ExclusiveGroups.Any(group => exclusiveGroups.Contains(group)))
                {
                    conflicts.Add(affix.AffixInstanceId);
                }
            }

            if (conflicts.Count > 0)
            {
                failed.Add("exclusive-group-conflict");
            }

            return Eligibility(failed.Count == 0, reasons, failed, tiers, conflicts);
        }

        public ItemQualityAffixOperationResult ApplyAffix(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            ItemAffixDefinition definition,
            string tierId = "",
            string affixInstanceId = "",
            string seed = "",
            ItemAffixSource source = ItemAffixSource.Authored,
            bool hidden = false,
            string generationPolicyId = "",
            bool preview = false)
        {
            if (definition == null)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, "Affix definition is missing.");
            }

            ItemAffixEligibilityResult eligibility = EvaluateAffixEligibility(itemRuntime, compositionRuntime, registry, itemInstanceId, definition);
            if (!eligibility.Eligible)
            {
                return ItemQualityAffixOperationResult.Failure(eligibility.ConflictingAffixIds.Count > 0 ? ItemQualityAffixOperationStatus.Conflict : ItemQualityAffixOperationStatus.Ineligible, $"Affix '{definition.Id}' is not eligible: {string.Join(",", eligibility.FailedRequirements)}.");
            }

            float quality = TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot qualitySnapshot) ? qualitySnapshot.OverallQuality : 0.5f;
            ItemAffixTierData tier = string.IsNullOrWhiteSpace(tierId)
                ? definition.ResolveBestTier(quality)
                : definition.Tiers.FirstOrDefault(candidate => string.Equals(candidate.tierId, tierId, StringComparison.Ordinal));
            if (tier == null)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.InvalidRequest, $"Affix '{definition.Id}' has no compatible tier.");
            }

            ItemAffixInstanceData instance = CreateAffixInstance(itemInstanceId, definition, tier, seed, source, hidden || definition.HiddenByDefault);
            if (!string.IsNullOrWhiteSpace(generationPolicyId))
            {
                instance.generationPolicyId = generationPolicyId;
            }

            if (!ValidateAffixInstance(instance, registry, itemRuntime, out string failure))
            {
                return ItemQualityAffixOperationResult.Failure(ToStatus(failure), failure);
            }

            if (string.IsNullOrWhiteSpace(affixInstanceId))
            {
                instance.affixInstanceId = AffixInstanceId(itemInstanceId, definition.Id, tier.tierId, seed);
            }
            else
            {
                instance.affixInstanceId = affixInstanceId;
                instance.modifierSourceId = ModifierSourceId(instance.affixInstanceId);
            }

            if (affixesById.ContainsKey(instance.affixInstanceId))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.DuplicateAffix, $"Affix instance '{instance.affixInstanceId}' already exists.");
            }

            if (preview)
            {
                return ItemQualityAffixOperationResult.Success(qualitySnapshot, "Affix application preview prepared.", true, new[] { new ItemAffixSnapshot(instance) });
            }

            instance.revisionHistory.Add(new ItemQualityRevisionData { revision = instance.revision, operationId = "affix.apply", sourceId = source.ToString(), message = "Affix applied." });
            affixesById.Add(instance.affixInstanceId, instance);
            UpdateRarityForItem(itemInstanceId, registry);
            revision++;
            ItemAffixStateChanged?.Invoke(itemInstanceId);
            return ItemQualityAffixOperationResult.Success(qualitySnapshot, "Affix applied.", affixes: new[] { new ItemAffixSnapshot(instance) });
        }

        public ItemQualityAffixOperationResult GenerateAffixes(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            ItemAffixGenerationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemInstanceId))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.InvalidRequest, "Affix generation requires an item instance ID.");
            }

            if (itemRuntime == null || !itemRuntime.TryGetSnapshot(request.ItemInstanceId, out _))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingItem, $"Item instance '{request.ItemInstanceId}' was not found.");
            }

            EnsureDefaultQuality(itemRuntime, compositionRuntime, registry, request.ItemInstanceId);
            int count = Mathf.Max(0, request.RequestedAffixCount);
            if (count == 0)
            {
                return ItemQualityAffixOperationResult.Success(null, "No affixes requested.", request.Preview, Array.Empty<ItemAffixSnapshot>());
            }

            string seed = string.IsNullOrWhiteSpace(request.Seed) ? "0" : request.Seed;
            string policyId = string.IsNullOrWhiteSpace(request.PolicyId) ? "affix-policy.prototype.default" : request.PolicyId;
            if (!request.Preview && HasExistingGeneratedAffixes(request.ItemInstanceId, policyId, seed, request.Source))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.DuplicateAffix, $"Affix generation request '{policyId}:{seed}' is already applied to item '{request.ItemInstanceId}'.");
            }

            ItemQualityAffixRuntime working = CloneRuntime();
            List<ItemAffixSnapshot> selected = new List<ItemAffixSnapshot>();
            IReadOnlyList<ItemAffixDefinition> definitions = CandidateAffixes(registry, request)
                .OrderBy(definition => DeterministicScore(seed, request.ItemInstanceId, definition.Id))
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();

            foreach (ItemAffixDefinition definition in definitions)
            {
                if (selected.Count >= count)
                {
                    break;
                }

                string perAffixSeed = $"{seed}:{selected.Count}:{definition.Id}";
                ItemQualityAffixOperationResult applied = working.ApplyAffix(itemRuntime, compositionRuntime, registry, request.ItemInstanceId, definition, seed: perAffixSeed, source: request.Source, generationPolicyId: policyId, preview: false);
                if (applied.Succeeded)
                {
                    selected.Add(applied.Affixes[0]);
                }
            }

            if (selected.Count < count)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.Ineligible, $"Generated {selected.Count} of {count} requested affix(es).");
            }

            if (request.Preview)
            {
                return ItemQualityAffixOperationResult.Success(working.TryGetQualityForItem(request.ItemInstanceId, out ItemQualitySnapshot previewQuality) ? previewQuality : null, "Affix generation preview prepared.", true, selected);
            }

            ItemQualityAffixRuntimeSaveData prepared = working.CreateSaveData();
            ItemQualityAffixOperationResult restore = RestoreFromSaveData(prepared, registry, itemRuntime);
            if (!restore.Succeeded)
            {
                return restore;
            }

            ItemAffixStateChanged?.Invoke(request.ItemInstanceId);
            return ItemQualityAffixOperationResult.Success(TryGetQualityForItem(request.ItemInstanceId, out ItemQualitySnapshot committedQuality) ? committedQuality : null, "Affixes generated.", false, selected);
        }

        public ItemQualityAffixOperationResult SetAffixActive(string affixInstanceId, bool active, bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(affixInstanceId) || !affixesById.TryGetValue(affixInstanceId, out ItemAffixInstanceData current))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, $"Affix instance '{affixInstanceId}' was not found.");
            }

            if (current.removed)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.InvalidRequest, $"Affix instance '{affixInstanceId}' has been removed.");
            }

            ItemAffixInstanceData working = current.Clone();
            working.active = active;
            working.revision = current.revision + 1L;
            working.revisionHistory.Add(new ItemQualityRevisionData { revision = working.revision, operationId = active ? "affix.enable" : "affix.disable", message = active ? "Affix enabled." : "Affix disabled." });
            if (preview)
            {
                return ItemQualityAffixOperationResult.Success(null, "Affix state preview prepared.", true, new[] { new ItemAffixSnapshot(working) });
            }

            affixesById[affixInstanceId] = working;
            revision++;
            ItemAffixStateChanged?.Invoke(working.itemInstanceId);
            return ItemQualityAffixOperationResult.Success(null, active ? "Affix enabled." : "Affix disabled.", affixes: new[] { new ItemAffixSnapshot(working) });
        }

        public ItemQualityAffixOperationResult RemoveAffix(string affixInstanceId, DefinitionRegistry registry = null, bool preview = false)
        {
            if (string.IsNullOrWhiteSpace(affixInstanceId) || !affixesById.TryGetValue(affixInstanceId, out ItemAffixInstanceData current))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, $"Affix instance '{affixInstanceId}' was not found.");
            }

            ItemAffixInstanceData removed = current.Clone();
            removed.active = false;
            removed.removed = true;
            removed.revision = current.revision + 1L;
            removed.revisionHistory.Add(new ItemQualityRevisionData { revision = removed.revision, operationId = "affix.remove", message = "Affix removed." });
            if (preview)
            {
                return ItemQualityAffixOperationResult.Success(null, "Affix removal preview prepared.", true, new[] { new ItemAffixSnapshot(removed) });
            }

            affixesById[affixInstanceId] = removed;
            UpdateRarityForItem(current.itemInstanceId, registry);
            revision++;
            ItemAffixStateChanged?.Invoke(current.itemInstanceId);
            return ItemQualityAffixOperationResult.Success(null, "Affix removed.", affixes: new[] { new ItemAffixSnapshot(removed) });
        }

        public ItemQualityProjection Project(string itemInstanceId, InformationAccessDecision decision = null)
        {
            if (!TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality))
            {
                return new ItemQualityProjection(null, denied: true, redacted: false, Array.Empty<ItemAffixSnapshot>(), Array.Empty<string>());
            }

            bool denied = decision != null && (decision.Decision == InformationAccessDecisionKind.Denied || decision.Decision == InformationAccessDecisionKind.MissingAuthorization);
            bool redacted = denied || decision != null && (decision.Decision == InformationAccessDecisionKind.RedactedAccess || decision.Decision == InformationAccessDecisionKind.PartialAccess);
            if (denied)
            {
                return new ItemQualityProjection(null, denied: true, redacted: true, Array.Empty<ItemAffixSnapshot>(), ItemQualityAffixInformationSubject.ProtectedFields);
            }

            if (!redacted)
            {
                return new ItemQualityProjection(quality, denied: false, redacted: false, GetAffixesForItem(itemInstanceId), Array.Empty<string>());
            }

            ItemQualityRecordData qualityData = quality.Data.Clone();
            qualityData.accessPolicyId = string.Empty;
            qualityData.provenanceId = string.Empty;
            qualityData.deterministicSeed = string.Empty;
            qualityData.revisionHistory = new List<ItemQualityRevisionData>();
            bool containsHiddenAffix = GetAffixesForItem(itemInstanceId).Any(affix => affix.Hidden);
            if (containsHiddenAffix && qualityData.rarity != null)
            {
                qualityData.rarity.derivedRarityId = string.Empty;
                qualityData.rarity.derivedScore = -1f;
                qualityData.rarity.policyId = string.Empty;
            }

            qualityData.defects = qualityData.defects.Where(defect => defect != null && !defect.hidden).Select(defect =>
            {
                ItemDefectEntryData clone = defect.Clone();
                clone.accessPolicyId = string.Empty;
                clone.provenanceId = string.Empty;
                return clone;
            }).ToList();
            IReadOnlyList<ItemAffixSnapshot> visibleAffixes = GetAffixesForItem(itemInstanceId)
                .Where(affix => !affix.Hidden)
                .Select(affix =>
                {
                    ItemAffixInstanceData clone = affix.Data.Clone();
                    clone.generationSeed = string.Empty;
                    clone.modifierSourceId = string.Empty;
                    clone.provenanceId = string.Empty;
                    clone.accessPolicyId = string.Empty;
                    clone.revisionHistory = new List<ItemQualityRevisionData>();
                    return new ItemAffixSnapshot(clone);
                })
                .ToArray();
            return new ItemQualityProjection(new ItemQualitySnapshot(qualityData), denied: false, redacted: true, visibleAffixes, ItemQualityAffixInformationSubject.ProtectedFields);
        }

        public bool CanShareQualityAffixStack(string leftItemInstanceId, string rightItemInstanceId)
        {
            return QualityAffixSignature(leftItemInstanceId) == QualityAffixSignature(rightItemInstanceId);
        }

        public string QualityAffixSignature(string itemInstanceId)
        {
            TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality);
            string qualityPart = quality == null
                ? "quality:none"
                : $"{quality.QualityTierId}:{quality.OverallQuality:0.######}:{string.Join(",", quality.Data.workmanship.OrderBy(entry => entry.entryId, StringComparer.Ordinal).Select(entry => $"{entry.dimension}:{entry.value.state}:{entry.value.value:0.######}:{entry.componentEntryId}"))}:{string.Join(",", quality.Data.defects.OrderBy(entry => entry.defectId, StringComparer.Ordinal).Select(entry => $"{entry.defectId}:{entry.category}:{entry.severity:0.######}:{entry.hidden}:{entry.active}"))}";
            string affixPart = string.Join(",", GetAffixesForItem(itemInstanceId)
                .Select(affix => affix.Data)
                .OrderBy(affix => affix.affixDefinitionId, StringComparer.Ordinal)
                .ThenBy(affix => affix.affixTierId, StringComparer.Ordinal)
                .ThenBy(affix => affix.affixInstanceId, StringComparer.Ordinal)
                .Select(affix => $"{affix.affixDefinitionId}:{affix.affixTierId}:{affix.classification}:{affix.active}:{affix.hidden}:{string.Join("/", affix.rolledValues.OrderBy(value => value.valueId, StringComparer.Ordinal).Select(value => $"{value.valueId}:{value.value:0.######}:{value.unit}"))}"));
            return $"{qualityPart}|affixes:{affixPart}";
        }

        public ItemQualityAffixOperationResult ApplyActiveAffixModifiers(
            string itemInstanceId,
            DefinitionRegistry registry,
            IRuntimeCalculatedStatReceiver statReceiver,
            out IReadOnlyList<StatModifierSource> appliedSources)
        {
            appliedSources = Array.Empty<StatModifierSource>();
            if (statReceiver == null)
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingRuntime, "Runtime stat receiver is missing.");
            }

            List<RuntimeCalculatedStatContribution> prepared = new List<RuntimeCalculatedStatContribution>();
            List<StatModifierSource> sources = new List<StatModifierSource>();
            foreach (ItemAffixSnapshot affix in GetAffixesForItem(itemInstanceId, activeOnly: true))
            {
                if (registry == null || !registry.TryGet(affix.AffixDefinitionId, out ItemAffixDefinition definition))
                {
                    return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, $"Affix definition '{affix.AffixDefinitionId}' is missing.");
                }

                ItemAffixTierData tier = definition.Tiers.FirstOrDefault(candidate => string.Equals(candidate.tierId, affix.AffixTierId, StringComparison.Ordinal));
                if (tier == null)
                {
                    return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, $"Affix tier '{affix.AffixTierId}' is missing.");
                }

                StatModifierSource source = new StatModifierSource(StatModifierSourceType.Equipment, affix.Data.modifierSourceId);
                int modifierIndex = 0;
                foreach (CalculatedStatModifierDefinition modifier in tier.modifierTemplates ?? Array.Empty<CalculatedStatModifierDefinition>())
                {
                    if (modifier == null || !modifier.IsValid)
                    {
                        return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.ValidationFailed, $"Affix '{affix.AffixDefinitionId}' has an invalid stat modifier template.");
                    }

                    prepared.Add(modifier.CreateRuntimeContribution(source, 1, $"affix.{affix.Data.affixInstanceId}.{modifierIndex++}.{modifier.Stat.Id}"));
                    if (!sources.Contains(source))
                    {
                        sources.Add(source);
                    }
                }
            }

            List<StatModifierSource> addedSources = new List<StatModifierSource>();
            for (int i = 0; i < prepared.Count; i++)
            {
                RuntimeCalculatedStatContribution modifier = prepared[i];
                if (!statReceiver.AddCalculatedStatContribution(modifier))
                {
                    for (int j = 0; j < addedSources.Count; j++)
                    {
                        statReceiver.RemoveCalculatedStatContributions(addedSources[j]);
                    }

                    return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.AtomicCommitFailed, $"Affix modifier source '{modifier.sourceId}' could not be applied; added sources were rolled back.");
                }

                StatModifierSource source = new StatModifierSource(StatModifierSourceType.Equipment, modifier.sourceId);
                if (!addedSources.Contains(source))
                {
                    addedSources.Add(source);
                }
            }

            appliedSources = addedSources.ToArray();
            return ItemQualityAffixOperationResult.Success(TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality) ? quality : null, "Affix modifiers applied.");
        }

        public ItemQualityAffixRuntimeSaveData CreateSaveData()
        {
            return new ItemQualityAffixRuntimeSaveData
            {
                schemaVersion = ItemQualityAffixRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                qualityRecords = qualityById.Values.OrderBy(record => record.qualityRecordId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                affixInstances = affixesById.Values.OrderBy(record => record.affixInstanceId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public ItemQualityAffixOperationResult RestoreFromSaveData(ItemQualityAffixRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime)
        {
            if (!ValidateSaveData(saveData, registry, itemRuntime, out string failure))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.RestoreFailed, failure);
            }

            qualityById.Clear();
            qualityIdByItemId.Clear();
            affixesById.Clear();
            foreach (ItemQualityRecordData record in saveData.qualityRecords.Select(record => record.Clone()))
            {
                qualityById.Add(record.qualityRecordId, record);
                qualityIdByItemId.Add(record.itemInstanceId, record.qualityRecordId);
            }

            foreach (ItemAffixInstanceData affix in saveData.affixInstances.Select(record => record.Clone()))
            {
                affixesById.Add(affix.affixInstanceId, affix);
            }

            revision = Math.Max(0L, saveData.revision);
            return ItemQualityAffixOperationResult.Success(null, "Item quality and affixes restored.");
        }

        public ItemQualityAffixOperationResult SetAuthoredRarityOverride(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            DefinitionRegistry registry,
            string itemInstanceId,
            string rarityDefinitionId,
            bool preview = false)
        {
            ItemQualityAffixOperationResult ensured = EnsureDefaultQuality(itemRuntime, compositionRuntime, registry, itemInstanceId, preview: false);
            if (!ensured.Succeeded)
            {
                return ensured;
            }

            if (!string.IsNullOrWhiteSpace(rarityDefinitionId)
                && registry != null
                && !registry.TryGet(rarityDefinitionId, out RarityDefinition _))
            {
                return ItemQualityAffixOperationResult.Failure(ItemQualityAffixOperationStatus.MissingDefinition, $"Rarity definition '{rarityDefinitionId}' was not found.");
            }

            ItemQualityRecordData record = ensured.Quality.Data.Clone();
            record.rarity ??= new ItemRarityStateData();
            record.rarity.authoredOverrideRarityId = rarityDefinitionId ?? string.Empty;
            record.rarity.source = string.IsNullOrWhiteSpace(rarityDefinitionId) ? ItemRaritySource.Derived : ItemRaritySource.AuthoredOverride;
            return SetQualityRecord(itemRuntime, compositionRuntime, registry, record, preview);
        }

        public static bool ValidateSaveData(ItemQualityAffixRuntimeSaveData saveData, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Item quality payload is missing.";
                return false;
            }

            if (saveData.schemaVersion != ItemQualityAffixRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported item quality schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> qualityIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> qualityItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemQualityRecordData record in saveData.qualityRecords ?? new List<ItemQualityRecordData>())
            {
                if (!ValidateQualityRecord(record, registry, itemRuntime, null, out failure))
                {
                    return false;
                }

                if (!qualityIds.Add(record.qualityRecordId) || !qualityItemIds.Add(record.itemInstanceId))
                {
                    failure = $"Duplicate quality record for item '{record.itemInstanceId}'.";
                    return false;
                }
            }

            HashSet<string> affixIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemAffixInstanceData affix in saveData.affixInstances ?? new List<ItemAffixInstanceData>())
            {
                if (!ValidateAffixInstance(affix, registry, itemRuntime, out failure))
                {
                    return false;
                }

                if (!affixIds.Add(affix.affixInstanceId))
                {
                    failure = $"Duplicate affix instance '{affix.affixInstanceId}'.";
                    return false;
                }
            }

            return true;
        }

        private ItemQualityAffixRuntime CloneRuntime()
        {
            ItemQualityAffixRuntime clone = new ItemQualityAffixRuntime();
            clone.RestoreFromSaveData(CreateSaveData(), null, null);
            return clone;
        }

        private void ApplyEvaluation(ItemQualityRecordData record, DefinitionRegistry registry, ItemCompositionRuntime compositionRuntime)
        {
            if (record == null)
            {
                return;
            }

            float weighted = 0f;
            float weight = 0f;
            foreach (ItemQualityDimensionEntryData entry in (record.dimensions ?? new List<ItemQualityDimensionEntryData>()).Where(entry => entry?.value?.state == QualityValueState.Known))
            {
                float entryWeight = Mathf.Max(0f, entry.weight);
                weighted += Mathf.Clamp01(entry.value.value) * entryWeight;
                weight += entryWeight;
            }

            foreach (ItemWorkmanshipEntryData entry in (record.workmanship ?? new List<ItemWorkmanshipEntryData>()).Where(entry => entry?.value?.state == QualityValueState.Known))
            {
                weighted += Mathf.Clamp01(entry.value.value);
                weight += 1f;
            }

            float defectPenalty = (record.defects ?? new List<ItemDefectEntryData>()).Where(defect => defect != null && defect.active).Sum(defect => Mathf.Clamp01(defect.severity) * 0.15f);
            if (weight > 0f)
            {
                record.overallQuality = Mathf.Clamp01(weighted / weight - defectPenalty);
            }
            else if (record.overallQuality >= 0f)
            {
                record.overallQuality = Mathf.Clamp01(record.overallQuality - defectPenalty);
            }

            record.qualityTierId = ResolveTier(record.overallQuality, registry);
            record.rarity ??= new ItemRarityStateData();
            record.rarity.derivedScore = Mathf.Clamp01(record.overallQuality + ActiveAffixRarity(record.itemInstanceId, registry));
            record.rarity.derivedRarityId = ResolveRarity(record.rarity.derivedScore, registry);
            if (record.rarity.source == ItemRaritySource.Unknown)
            {
                record.rarity.source = ItemRaritySource.Derived;
            }
        }

        private void UpdateRarityForItem(string itemInstanceId, DefinitionRegistry registry)
        {
            if (TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality))
            {
                ItemQualityRecordData record = quality.Data.Clone();
                ApplyEvaluation(record, registry, null);
                bool replacing = qualityById.ContainsKey(record.qualityRecordId);
                record.revision = Math.Max(1L, replacing ? qualityById[record.qualityRecordId].revision + 1L : record.revision);
                record.revisionHistory.Add(new ItemQualityRevisionData { revision = record.revision, operationId = "rarity.derive", sourceId = "affix", message = "Rarity derived from quality and affixes." });
                qualityById[record.qualityRecordId] = record;
            }
        }

        private static ItemAffixEligibilityResult Eligibility(bool eligible, List<string> reasons, List<string> failed, List<string> tiers, List<string> conflicts)
        {
            return new ItemAffixEligibilityResult
            {
                Eligible = eligible,
                PolicyId = "affix-eligibility.default",
                Reasons = reasons.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                FailedRequirements = failed.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CompatibleTierIds = tiers.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ConflictingAffixIds = conflicts.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        private static ItemAffixInstanceData CreateAffixInstance(string itemInstanceId, ItemAffixDefinition definition, ItemAffixTierData tier, string seed, ItemAffixSource source, bool hidden)
        {
            float rolled = tier.valueMinimum;
            if (tier.valueMaximum > tier.valueMinimum)
            {
                rolled = tier.valueMinimum + (tier.valueMaximum - tier.valueMinimum) * DeterministicScore(seed, itemInstanceId, definition.Id, tier.tierId);
            }

            string id = AffixInstanceId(itemInstanceId, definition.Id, tier.tierId, seed);
            return new ItemAffixInstanceData
            {
                affixInstanceId = id,
                itemInstanceId = itemInstanceId,
                affixDefinitionId = definition.Id,
                affixTierId = tier.tierId,
                classification = definition.Classification,
                source = source,
                generationSeed = seed ?? string.Empty,
                generationPolicyId = "affix-policy.prototype.default",
                modifierSourceId = ModifierSourceId(id),
                hidden = hidden,
                identified = !hidden,
                accessPolicyId = definition.AccessPolicyId,
                rolledValues = { new ItemAffixValueData { valueId = "value.primary", value = rolled, unit = "normalized" } },
                tags = CloneIds(definition.Tags.Where(tag => tag != null).Select(tag => tag.Id).Concat(tier.tags))
            };
        }

        private static IReadOnlyList<ItemAffixDefinition> CandidateAffixes(DefinitionRegistry registry, ItemAffixGenerationRequest request)
        {
            HashSet<ItemAffixClassification> allowed = new HashSet<ItemAffixClassification>(request.AllowedClassifications ?? Array.Empty<ItemAffixClassification>());
            return registry?.DefinitionsById.Values
                .OfType<ItemAffixDefinition>()
                .Where(definition => definition.GenerationWeight > 0f)
                .Where(definition => allowed.Count == 0 || allowed.Contains(definition.Classification))
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<ItemAffixDefinition>();
        }

        private bool HasExistingGeneratedAffixes(string itemInstanceId, string policyId, string seed, ItemAffixSource source)
        {
            string seedPrefix = $"{seed}:";
            return affixesById.Values.Any(affix =>
                affix != null
                && !affix.removed
                && string.Equals(affix.itemInstanceId, itemInstanceId, StringComparison.Ordinal)
                && affix.source == source
                && string.Equals(affix.generationPolicyId, policyId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(affix.generationSeed)
                && affix.generationSeed.StartsWith(seedPrefix, StringComparison.Ordinal));
        }

        private HashSet<string> MaterialTags(ItemCompositionRuntime compositionRuntime, DefinitionRegistry registry, string itemInstanceId)
        {
            HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
            if (compositionRuntime == null || registry == null || !compositionRuntime.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition))
            {
                return tags;
            }

            foreach (string materialId in composition.Materials.Select(material => material.materialDefinitionId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
            {
                if (registry.TryGet(materialId, out MaterialDefinition material))
                {
                    foreach (string tag in material.MaterialTags)
                    {
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }
            }

            return tags;
        }

        private float ActiveAffixRarity(string itemInstanceId, DefinitionRegistry registry)
        {
            float contribution = 0f;
            foreach (ItemAffixSnapshot affix in GetAffixesForItem(itemInstanceId, activeOnly: true))
            {
                if (registry != null && registry.TryGet(affix.AffixDefinitionId, out ItemAffixDefinition definition))
                {
                    contribution += Mathf.Max(0f, definition.RarityContribution);
                    ItemAffixTierData tier = definition.Tiers.FirstOrDefault(candidate => string.Equals(candidate.tierId, affix.AffixTierId, StringComparison.Ordinal));
                    contribution += Mathf.Max(0f, tier?.rarityContribution ?? 0f);
                }
            }

            return contribution;
        }

        private static string ResolveTier(float quality, DefinitionRegistry registry)
        {
            QualityTierDefinition tier = registry?.DefinitionsById.Values
                .OfType<QualityTierDefinition>()
                .Where(candidate => candidate.Contains(quality))
                .OrderByDescending(candidate => candidate.SortOrder)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            return tier?.Id ?? (registry?.Defaults?.DefaultQualityTier as QualityTierDefinition)?.Id ?? string.Empty;
        }

        private static string ResolveRarity(float score, DefinitionRegistry registry)
        {
            RarityDefinition[] rarities = registry?.DefinitionsById.Values.OfType<RarityDefinition>().OrderBy(rarity => rarity.Rank).ThenBy(rarity => rarity.Id, StringComparer.Ordinal).ToArray()
                ?? Array.Empty<RarityDefinition>();
            if (rarities.Length == 0) return registry?.Defaults?.DefaultRarity?.Id ?? string.Empty;

            int index = Mathf.Clamp(Mathf.FloorToInt(score * rarities.Length), 0, rarities.Length - 1);
            return rarities[index].Id;
        }

        private static void NormalizeQuality(ItemQualityRecordData record)
        {
            if (record == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(record.qualityRecordId) && !string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                record.qualityRecordId = QualityRecordId(record.itemInstanceId);
            }

            record.workmanship ??= new List<ItemWorkmanshipEntryData>();
            record.dimensions ??= new List<ItemQualityDimensionEntryData>();
            record.componentQualities ??= new List<ItemComponentQualityEntryData>();
            record.defects ??= new List<ItemDefectEntryData>();
            record.revisionHistory ??= new List<ItemQualityRevisionData>();
            record.tags = CloneIds((record.tags ?? Array.Empty<string>()).Concat(new[] { "item.quality" }));
        }

        private static bool ValidateQualityRecord(ItemQualityRecordData record, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemCompositionRuntime compositionRuntime, out string failure)
        {
            failure = string.Empty;
            if (record == null)
            {
                failure = "Quality record is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.qualityRecordId) || string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                failure = "Quality record requires quality and item instance IDs.";
                return false;
            }

            if (itemRuntime != null && !itemRuntime.TryGetSnapshot(record.itemInstanceId, out ItemInstanceSnapshot item))
            {
                failure = $"Quality record '{record.qualityRecordId}' references missing item '{record.itemInstanceId}'.";
                return false;
            }

            if (itemRuntime != null && itemRuntime.TryGetSnapshot(record.itemInstanceId, out item)
                && !string.IsNullOrWhiteSpace(record.itemDefinitionId)
                && !string.Equals(record.itemDefinitionId, item.ItemDefinitionId, StringComparison.Ordinal))
            {
                failure = $"Quality record '{record.qualityRecordId}' definition '{record.itemDefinitionId}' does not match item '{item.ItemDefinitionId}'.";
                return false;
            }

            if (float.IsNaN(record.overallQuality) || float.IsInfinity(record.overallQuality) || (record.overallQuality < 0f && record.overallQuality != -1f) || record.overallQuality > 1f)
            {
                failure = $"Quality record '{record.qualityRecordId}' has invalid overall quality.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ItemQualityRecordSource), record.source))
            {
                failure = $"Quality record '{record.qualityRecordId}' has invalid source.";
                return false;
            }

            if (!ValidateEntryList(record.workmanship, entry => entry.entryId, "workmanship", out failure) ||
                !ValidateEntryList(record.dimensions, entry => entry.entryId, "quality dimension", out failure) ||
                !ValidateEntryList(record.componentQualities, entry => entry.entryId, "component quality", out failure) ||
                !ValidateEntryList(record.defects, entry => entry.defectId, "defect", out failure))
            {
                return false;
            }

            foreach (ItemWorkmanshipEntryData entry in record.workmanship ?? new List<ItemWorkmanshipEntryData>())
            {
                if (!ValidateValue(entry?.value, out failure) || !Enum.IsDefined(typeof(WorkmanshipDimension), entry.dimension))
                {
                    failure = $"Workmanship entry '{entry?.entryId}': {failure}";
                    return false;
                }
            }

            foreach (ItemQualityDimensionEntryData entry in record.dimensions ?? new List<ItemQualityDimensionEntryData>())
            {
                if (!ValidateValue(entry?.value, out failure) || !Enum.IsDefined(typeof(ItemQualityDimension), entry.dimension) || float.IsNaN(entry.weight) || float.IsInfinity(entry.weight) || entry.weight < 0f)
                {
                    failure = $"Quality dimension entry '{entry?.entryId}' is invalid.";
                    return false;
                }
            }

            foreach (ItemDefectEntryData defect in record.defects ?? new List<ItemDefectEntryData>())
            {
                if (defect == null || !Enum.IsDefined(typeof(ItemDefectCategory), defect.category) || float.IsNaN(defect.severity) || float.IsInfinity(defect.severity) || defect.severity < 0f || defect.severity > 1f)
                {
                    failure = $"Defect '{defect?.defectId}' is invalid.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateAffixInstance(ItemAffixInstanceData affix, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            if (affix == null || string.IsNullOrWhiteSpace(affix.affixInstanceId) || string.IsNullOrWhiteSpace(affix.itemInstanceId) || string.IsNullOrWhiteSpace(affix.affixDefinitionId))
            {
                failure = "Affix instance requires instance, item, and definition IDs.";
                return false;
            }

            if (itemRuntime != null && !itemRuntime.TryGetSnapshot(affix.itemInstanceId, out _))
            {
                failure = $"Affix instance '{affix.affixInstanceId}' references missing item '{affix.itemInstanceId}'.";
                return false;
            }

            if (registry != null && !registry.TryGet(affix.affixDefinitionId, out ItemAffixDefinition _))
            {
                failure = $"Affix instance '{affix.affixInstanceId}' references missing affix definition '{affix.affixDefinitionId}'.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ItemAffixClassification), affix.classification) || !Enum.IsDefined(typeof(ItemAffixSource), affix.source))
            {
                failure = $"Affix instance '{affix.affixInstanceId}' has invalid classification or source.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(affix.modifierSourceId))
            {
                failure = $"Affix instance '{affix.affixInstanceId}' has no modifier source ID.";
                return false;
            }

            foreach (ItemAffixValueData value in affix.rolledValues ?? new List<ItemAffixValueData>())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.valueId) || float.IsNaN(value.value) || float.IsInfinity(value.value))
                {
                    failure = $"Affix instance '{affix.affixInstanceId}' has an invalid rolled value.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateValue(ItemQualityValueData value, out string failure)
        {
            failure = string.Empty;
            if (value == null || !Enum.IsDefined(typeof(QualityValueState), value.state))
            {
                failure = "value state is invalid";
                return false;
            }

            if (value.state == QualityValueState.Known && (float.IsNaN(value.value) || float.IsInfinity(value.value) || value.value < 0f || value.value > 1f))
            {
                failure = "known value must be within 0..1";
                return false;
            }

            if (value.state != QualityValueState.Known && value.value != -1f)
            {
                failure = "unknown and not-applicable values must use -1";
                return false;
            }

            return true;
        }

        private static bool ValidateEntryList<T>(IEnumerable<T> entries, Func<T, string> idSelector, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T entry in entries ?? Array.Empty<T>())
            {
                string id = idSelector(entry);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    failure = $"Missing or duplicate {label} entry ID '{id}'.";
                    return false;
                }
            }

            return true;
        }

        private static ItemQualityAffixOperationStatus ToStatus(string failure)
        {
            string text = failure ?? string.Empty;
            if (text.Contains("missing item", StringComparison.OrdinalIgnoreCase)) return ItemQualityAffixOperationStatus.MissingItem;
            if (text.Contains("missing affix", StringComparison.OrdinalIgnoreCase) || text.Contains("missing definition", StringComparison.OrdinalIgnoreCase)) return ItemQualityAffixOperationStatus.MissingDefinition;
            if (text.Contains("duplicate", StringComparison.OrdinalIgnoreCase)) return ItemQualityAffixOperationStatus.DuplicateRecord;
            if (text.Contains("invalid", StringComparison.OrdinalIgnoreCase)) return ItemQualityAffixOperationStatus.InvalidValue;
            return ItemQualityAffixOperationStatus.ValidationFailed;
        }

        private static ItemWorkmanshipEntryData Workmanship(string id, WorkmanshipDimension dimension, float value, QualityValueState state)
        {
            return new ItemWorkmanshipEntryData
            {
                entryId = id,
                dimension = dimension,
                value = new ItemQualityValueData { state = state, value = state == QualityValueState.Known ? Mathf.Clamp01(value) : -1f }
            };
        }

        private static ItemQualityDimensionEntryData Dimension(string id, ItemQualityDimension dimension, float value, QualityValueState state, float weight)
        {
            return new ItemQualityDimensionEntryData
            {
                entryId = id,
                dimension = dimension,
                value = new ItemQualityValueData { state = state, value = state == QualityValueState.Known ? Mathf.Clamp01(value) : -1f },
                weight = Mathf.Max(0f, weight)
            };
        }

        private static float DeterministicScore(params string[] parts)
        {
            unchecked
            {
                int hash = 17;
                foreach (string part in parts ?? Array.Empty<string>())
                {
                    string text = part ?? string.Empty;
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash = hash * 31 + text[i];
                    }
                }

                uint positive = (uint)hash;
                return (positive % 1000000u) / 999999f;
            }
        }

        private static string QualityRecordId(string itemInstanceId)
        {
            return $"item-quality.{itemInstanceId}";
        }

        private static string AffixInstanceId(string itemInstanceId, string definitionId, string tierId, string seed)
        {
            string key = $"{itemInstanceId}.{definitionId}.{tierId}.{seed}".Replace(":", ".").Replace("/", ".").Replace(" ", "-");
            return $"item-affix.{key}";
        }

        private static string ModifierSourceId(string affixInstanceId)
        {
            return $"item-affix-modifier.{affixInstanceId}";
        }
    }
}
