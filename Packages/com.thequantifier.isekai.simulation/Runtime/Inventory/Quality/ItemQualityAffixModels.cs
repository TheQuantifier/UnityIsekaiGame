using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Stats;
using static UnityIsekaiGame.Inventory.Quality.ItemQualityAffixCloneUtility;

namespace UnityIsekaiGame.Inventory.Quality
{
    [Serializable]
    public sealed class ItemQualityValueData
    {
        public QualityValueState state = QualityValueState.Unknown;
        public float value = -1f;
        public string classificationId;

        public ItemQualityValueData Clone()
        {
            return new ItemQualityValueData
            {
                state = state,
                value = value,
                classificationId = classificationId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemWorkmanshipEntryData
    {
        public string entryId;
        public WorkmanshipDimension dimension = WorkmanshipDimension.Unknown;
        public ItemQualityValueData value = new ItemQualityValueData();
        public string componentEntryId;
        public string materialEntryId;
        public string sourceId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();

        public ItemWorkmanshipEntryData Clone()
        {
            return new ItemWorkmanshipEntryData
            {
                entryId = entryId ?? string.Empty,
                dimension = dimension,
                value = value?.Clone() ?? new ItemQualityValueData(),
                componentEntryId = componentEntryId ?? string.Empty,
                materialEntryId = materialEntryId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = CloneIds(tags)
            };
        }
    }

    [Serializable]
    public sealed class ItemQualityDimensionEntryData
    {
        public string entryId;
        public ItemQualityDimension dimension = ItemQualityDimension.Unknown;
        public ItemQualityValueData value = new ItemQualityValueData();
        public string componentEntryId;
        public string materialEntryId;
        public float weight = 1f;
        public string sourceId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();

        public ItemQualityDimensionEntryData Clone()
        {
            return new ItemQualityDimensionEntryData
            {
                entryId = entryId ?? string.Empty,
                dimension = dimension,
                value = value?.Clone() ?? new ItemQualityValueData(),
                componentEntryId = componentEntryId ?? string.Empty,
                materialEntryId = materialEntryId ?? string.Empty,
                weight = weight,
                sourceId = sourceId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = CloneIds(tags)
            };
        }
    }

    [Serializable]
    public sealed class ItemDefectEntryData
    {
        public string defectId;
        public ItemDefectCategory category = ItemDefectCategory.Unknown;
        public float severity;
        public string affectedComponentEntryId;
        public string affectedMaterialEntryId;
        public bool hidden;
        public bool active = true;
        public bool removableLater;
        public string accessPolicyId;
        public string sourceId;
        public string provenanceId;
        public string[] affectedQualityDimensions = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();

        public ItemDefectEntryData Clone()
        {
            return new ItemDefectEntryData
            {
                defectId = defectId ?? string.Empty,
                category = category,
                severity = severity,
                affectedComponentEntryId = affectedComponentEntryId ?? string.Empty,
                affectedMaterialEntryId = affectedMaterialEntryId ?? string.Empty,
                hidden = hidden,
                active = active,
                removableLater = removableLater,
                accessPolicyId = accessPolicyId ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                affectedQualityDimensions = CloneIds(affectedQualityDimensions),
                tags = CloneIds(tags)
            };
        }
    }

    [Serializable]
    public sealed class ItemComponentQualityEntryData
    {
        public string entryId;
        public string componentEntryId;
        public string qualityTierId;
        public string summary;
        public List<ItemWorkmanshipEntryData> workmanship = new List<ItemWorkmanshipEntryData>();
        public List<ItemQualityDimensionEntryData> dimensions = new List<ItemQualityDimensionEntryData>();
        public List<ItemDefectEntryData> defects = new List<ItemDefectEntryData>();
        public string provenanceId;
        public long revision = 1L;

        public ItemComponentQualityEntryData Clone()
        {
            return new ItemComponentQualityEntryData
            {
                entryId = entryId ?? string.Empty,
                componentEntryId = componentEntryId ?? string.Empty,
                qualityTierId = qualityTierId ?? string.Empty,
                summary = summary ?? string.Empty,
                workmanship = workmanship == null ? new List<ItemWorkmanshipEntryData>() : workmanship.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                dimensions = dimensions == null ? new List<ItemQualityDimensionEntryData>() : dimensions.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                defects = defects == null ? new List<ItemDefectEntryData>() : defects.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ItemQualityRevisionData
    {
        public long revision;
        public string operationId;
        public string sourceId;
        public string message;
        public string worldTime;

        public ItemQualityRevisionData Clone()
        {
            return new ItemQualityRevisionData
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
    public sealed class ItemRarityStateData
    {
        public string derivedRarityId;
        public float derivedScore;
        public string authoredOverrideRarityId;
        public ItemRaritySource source = ItemRaritySource.Unknown;
        public string policyId;

        public string EffectiveRarityId => string.IsNullOrWhiteSpace(authoredOverrideRarityId) ? derivedRarityId ?? string.Empty : authoredOverrideRarityId;

        public ItemRarityStateData Clone()
        {
            return new ItemRarityStateData
            {
                derivedRarityId = derivedRarityId ?? string.Empty,
                derivedScore = derivedScore,
                authoredOverrideRarityId = authoredOverrideRarityId ?? string.Empty,
                source = source,
                policyId = policyId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemQualityRecordData
    {
        public string qualityRecordId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public float overallQuality = -1f;
        public string qualityTierId;
        public ItemQualityRecordSource source = ItemQualityRecordSource.Unknown;
        public string generationPolicyId;
        public string deterministicSeed;
        public string creationWorldTime;
        public string lastRevisionWorldTime;
        public long relatedCompositionRevision;
        public string accessPolicyId;
        public string provenanceId;
        public ItemRarityStateData rarity = new ItemRarityStateData();
        public List<ItemWorkmanshipEntryData> workmanship = new List<ItemWorkmanshipEntryData>();
        public List<ItemQualityDimensionEntryData> dimensions = new List<ItemQualityDimensionEntryData>();
        public List<ItemComponentQualityEntryData> componentQualities = new List<ItemComponentQualityEntryData>();
        public List<ItemDefectEntryData> defects = new List<ItemDefectEntryData>();
        public string[] tags = Array.Empty<string>();
        public List<ItemQualityRevisionData> revisionHistory = new List<ItemQualityRevisionData>();
        public long revision = 1L;

        public ItemQualityRecordData Clone()
        {
            return new ItemQualityRecordData
            {
                qualityRecordId = qualityRecordId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                overallQuality = overallQuality,
                qualityTierId = qualityTierId ?? string.Empty,
                source = source,
                generationPolicyId = generationPolicyId ?? string.Empty,
                deterministicSeed = deterministicSeed ?? string.Empty,
                creationWorldTime = creationWorldTime ?? string.Empty,
                lastRevisionWorldTime = lastRevisionWorldTime ?? string.Empty,
                relatedCompositionRevision = relatedCompositionRevision,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                rarity = rarity?.Clone() ?? new ItemRarityStateData(),
                workmanship = workmanship == null ? new List<ItemWorkmanshipEntryData>() : workmanship.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                dimensions = dimensions == null ? new List<ItemQualityDimensionEntryData>() : dimensions.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                componentQualities = componentQualities == null ? new List<ItemComponentQualityEntryData>() : componentQualities.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                defects = defects == null ? new List<ItemDefectEntryData>() : defects.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                tags = CloneIds(tags),
                revisionHistory = revisionHistory == null ? new List<ItemQualityRevisionData>() : revisionHistory.Select(entry => entry?.Clone()).Where(entry => entry != null).OrderBy(entry => entry.revision).ThenBy(entry => entry.operationId, StringComparer.Ordinal).ToList(),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ItemAffixValueData
    {
        public string valueId;
        public float value;
        public string unit;

        public ItemAffixValueData Clone()
        {
            return new ItemAffixValueData
            {
                valueId = valueId ?? string.Empty,
                value = value,
                unit = unit ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemAffixTierData
    {
        public string tierId;
        public int sortOrder;
        public float minimumItemQuality;
        public float maximumItemQuality = 1f;
        public float valueMinimum;
        public float valueMaximum;
        public float rarityContribution;
        public int identificationDifficulty;
        public CalculatedStatModifierDefinition[] modifierTemplates = Array.Empty<CalculatedStatModifierDefinition>();
        public string[] requiredMaterialTags = Array.Empty<string>();
        public string[] requiredComponentRoles = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();

        public ItemAffixTierData Clone()
        {
            return new ItemAffixTierData
            {
                tierId = tierId ?? string.Empty,
                sortOrder = sortOrder,
                minimumItemQuality = minimumItemQuality,
                maximumItemQuality = maximumItemQuality,
                valueMinimum = valueMinimum,
                valueMaximum = valueMaximum,
                rarityContribution = rarityContribution,
                identificationDifficulty = identificationDifficulty,
                modifierTemplates = modifierTemplates == null ? Array.Empty<CalculatedStatModifierDefinition>() : modifierTemplates.Where(entry => entry != null).ToArray(),
                requiredMaterialTags = CloneIds(requiredMaterialTags),
                requiredComponentRoles = CloneIds(requiredComponentRoles),
                tags = CloneIds(tags)
            };
        }
    }

    [Serializable]
    public sealed class ItemAffixInstanceData
    {
        public string affixInstanceId;
        public string itemInstanceId;
        public string affixDefinitionId;
        public string affixTierId;
        public ItemAffixClassification classification = ItemAffixClassification.Unknown;
        public ItemAffixSource source = ItemAffixSource.Unknown;
        public List<ItemAffixValueData> rolledValues = new List<ItemAffixValueData>();
        public string generationSeed;
        public string generationPolicyId;
        public string appliedWorldTime;
        public string applyingPersonId;
        public string relatedComponentEntryId;
        public string relatedMaterialEntryId;
        public long relatedCompositionRevision;
        public string modifierSourceId;
        public bool active = true;
        public bool removed;
        public bool hidden;
        public bool identified;
        public string accessPolicyId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public List<ItemQualityRevisionData> revisionHistory = new List<ItemQualityRevisionData>();
        public long revision = 1L;

        public ItemAffixInstanceData Clone()
        {
            return new ItemAffixInstanceData
            {
                affixInstanceId = affixInstanceId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                affixDefinitionId = affixDefinitionId ?? string.Empty,
                affixTierId = affixTierId ?? string.Empty,
                classification = classification,
                source = source,
                rolledValues = rolledValues == null ? new List<ItemAffixValueData>() : rolledValues.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                generationSeed = generationSeed ?? string.Empty,
                generationPolicyId = generationPolicyId ?? string.Empty,
                appliedWorldTime = appliedWorldTime ?? string.Empty,
                applyingPersonId = applyingPersonId ?? string.Empty,
                relatedComponentEntryId = relatedComponentEntryId ?? string.Empty,
                relatedMaterialEntryId = relatedMaterialEntryId ?? string.Empty,
                relatedCompositionRevision = relatedCompositionRevision,
                modifierSourceId = modifierSourceId ?? string.Empty,
                active = active,
                removed = removed,
                hidden = hidden,
                identified = identified,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = CloneIds(tags),
                revisionHistory = revisionHistory == null ? new List<ItemQualityRevisionData>() : revisionHistory.Select(entry => entry?.Clone()).Where(entry => entry != null).OrderBy(entry => entry.revision).ThenBy(entry => entry.operationId, StringComparer.Ordinal).ToList(),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ItemQualityAffixRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ItemQualityRecordData> qualityRecords = new List<ItemQualityRecordData>();
        public List<ItemAffixInstanceData> affixInstances = new List<ItemAffixInstanceData>();

        public ItemQualityAffixRuntimeSaveData Clone()
        {
            return new ItemQualityAffixRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                qualityRecords = qualityRecords == null ? new List<ItemQualityRecordData>() : qualityRecords.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                affixInstances = affixInstances == null ? new List<ItemAffixInstanceData>() : affixInstances.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList()
            };
        }
    }

    public sealed class ItemQualitySnapshot
    {
        public ItemQualitySnapshot(ItemQualityRecordData data)
        {
            Data = data?.Clone() ?? new ItemQualityRecordData();
        }

        public ItemQualityRecordData Data { get; }
        public string QualityRecordId => Data.qualityRecordId ?? string.Empty;
        public string ItemInstanceId => Data.itemInstanceId ?? string.Empty;
        public string QualityTierId => Data.qualityTierId ?? string.Empty;
        public float OverallQuality => Data.overallQuality;
        public long Revision => Data.revision;

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ItemQualityAffixInformationSubject.Quality(ItemInstanceId, QualityRecordId, Data.itemDefinitionId, Data.tags);
        }
    }

    public sealed class ItemAffixSnapshot
    {
        public ItemAffixSnapshot(ItemAffixInstanceData data)
        {
            Data = data?.Clone() ?? new ItemAffixInstanceData();
        }

        public ItemAffixInstanceData Data { get; }
        public string AffixInstanceId => Data.affixInstanceId ?? string.Empty;
        public string ItemInstanceId => Data.itemInstanceId ?? string.Empty;
        public string AffixDefinitionId => Data.affixDefinitionId ?? string.Empty;
        public string AffixTierId => Data.affixTierId ?? string.Empty;
        public bool Active => Data.active;
        public bool Hidden => Data.hidden;
        public long Revision => Data.revision;

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ItemQualityAffixInformationSubject.Affix(ItemInstanceId, AffixInstanceId, AffixDefinitionId, Data.tags);
        }
    }

    public sealed class ItemQualityAffixOperationResult
    {
        private ItemQualityAffixOperationResult(bool succeeded, bool preview, ItemQualityAffixOperationStatus status, string message, ItemQualitySnapshot quality, IReadOnlyList<ItemAffixSnapshot> affixes)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Quality = quality;
            Affixes = (affixes ?? Array.Empty<ItemAffixSnapshot>()).ToArray();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public ItemQualityAffixOperationStatus Status { get; }
        public string Message { get; }
        public ItemQualitySnapshot Quality { get; }
        public IReadOnlyList<ItemAffixSnapshot> Affixes { get; }

        public static ItemQualityAffixOperationResult Success(ItemQualitySnapshot quality, string message = "Item quality operation succeeded.", bool preview = false, IReadOnlyList<ItemAffixSnapshot> affixes = null)
        {
            return new ItemQualityAffixOperationResult(true, preview, preview ? ItemQualityAffixOperationStatus.Preview : ItemQualityAffixOperationStatus.Succeeded, message, quality, affixes);
        }

        public static ItemQualityAffixOperationResult Failure(ItemQualityAffixOperationStatus status, string message)
        {
            return new ItemQualityAffixOperationResult(false, false, status, message, null, Array.Empty<ItemAffixSnapshot>());
        }
    }

    public sealed class ItemQualityProjection
    {
        public ItemQualityProjection(ItemQualitySnapshot snapshot, bool denied, bool redacted, IReadOnlyList<ItemAffixSnapshot> affixes, IReadOnlyList<string> redactedFields)
        {
            Snapshot = snapshot;
            Denied = denied;
            Redacted = redacted;
            Affixes = (affixes ?? Array.Empty<ItemAffixSnapshot>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public ItemQualitySnapshot Snapshot { get; }
        public bool Denied { get; }
        public bool Redacted { get; }
        public IReadOnlyList<ItemAffixSnapshot> Affixes { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class ItemAffixEligibilityResult
    {
        public bool Eligible { get; set; }
        public string PolicyId { get; set; } = string.Empty;
        public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FailedRequirements { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> CompatibleTierIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ConflictingAffixIds { get; set; } = Array.Empty<string>();
    }

    public sealed class ItemAffixGenerationRequest
    {
        public string ItemInstanceId { get; set; } = string.Empty;
        public string PolicyId { get; set; } = "affix-policy.prototype.default";
        public string Seed { get; set; } = string.Empty;
        public int RequestedAffixCount { get; set; } = 1;
        public ItemAffixClassification[] AllowedClassifications { get; set; } = Array.Empty<ItemAffixClassification>();
        public ItemAffixSource Source { get; set; } = ItemAffixSource.Generated;
        public string CorrelationId { get; set; } = string.Empty;
        public bool Preview { get; set; }
    }

    public sealed class ItemQualityEvaluationResult
    {
        public bool Succeeded { get; set; }
        public float OverallQuality { get; set; }
        public string QualityTierId { get; set; } = string.Empty;
        public string DerivedRarityId { get; set; } = string.Empty;
        public float RarityScore { get; set; }
        public IReadOnlyList<string> ContributingInputs { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Diagnostics { get; set; } = Array.Empty<string>();
    }

    internal static class ItemQualityAffixCloneUtility
    {
        public static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
