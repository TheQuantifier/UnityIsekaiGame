using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Composition
{
    [Serializable]
    public sealed class MaterialQuantityData
    {
        public float value;
        public MaterialQuantityUnit unit = MaterialQuantityUnit.Count;

        public MaterialQuantityData Clone()
        {
            return new MaterialQuantityData { value = value, unit = unit };
        }
    }

    [Serializable]
    public sealed class ItemMaterialEntryData
    {
        public string entryId;
        public string materialDefinitionId;
        public string sourceItemDefinitionId;
        public MaterialEntryRole role = MaterialEntryRole.PrimaryStructure;
        public MaterialQuantityData quantity = new MaterialQuantityData { value = 1f, unit = MaterialQuantityUnit.Count };
        public float purity = 1f;
        public string processedForm;
        public int layerIndex;
        public string componentEntryId;
        public string accessPolicyId;
        public bool hidden;
        public string[] tags = Array.Empty<string>();

        public ItemMaterialEntryData Clone()
        {
            return new ItemMaterialEntryData
            {
                entryId = entryId ?? string.Empty,
                materialDefinitionId = materialDefinitionId ?? string.Empty,
                sourceItemDefinitionId = sourceItemDefinitionId ?? string.Empty,
                role = role,
                quantity = quantity?.Clone() ?? new MaterialQuantityData(),
                purity = purity,
                processedForm = processedForm ?? string.Empty,
                layerIndex = layerIndex,
                componentEntryId = componentEntryId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                hidden = hidden,
                tags = CloneIds(tags)
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemComponentEntryData
    {
        public string componentEntryId;
        public string parentComponentEntryId;
        public string componentName;
        public ItemComponentKind kind = ItemComponentKind.AbstractComponent;
        public string componentItemInstanceId;
        public string componentItemDefinitionId;
        public string[] materialEntryIds = Array.Empty<string>();
        public bool detachable;
        public bool replaceable;
        public bool optional;
        public int count = 1;
        public string accessPolicyId;
        public bool hidden;
        public string[] tags = Array.Empty<string>();

        public ItemComponentEntryData Clone()
        {
            return new ItemComponentEntryData
            {
                componentEntryId = componentEntryId ?? string.Empty,
                parentComponentEntryId = parentComponentEntryId ?? string.Empty,
                componentName = componentName ?? string.Empty,
                kind = kind,
                componentItemInstanceId = componentItemInstanceId ?? string.Empty,
                componentItemDefinitionId = componentItemDefinitionId ?? string.Empty,
                materialEntryIds = CloneIds(materialEntryIds),
                detachable = detachable,
                replaceable = replaceable,
                optional = optional,
                count = Math.Max(1, count),
                accessPolicyId = accessPolicyId ?? string.Empty,
                hidden = hidden,
                tags = CloneIds(tags)
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemCompositionRevisionData
    {
        public long revision;
        public string operationId;
        public string source;
        public string message;

        public ItemCompositionRevisionData Clone()
        {
            return new ItemCompositionRevisionData
            {
                revision = revision,
                operationId = operationId ?? string.Empty,
                source = source ?? string.Empty,
                message = message ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ItemCompositionRecordData
    {
        public string compositionId;
        public string itemInstanceId;
        public string sourceItemDefinitionId;
        public ItemCompositionCompleteness completeness = ItemCompositionCompleteness.Unknown;
        public string source;
        public string accessPolicyId;
        public string templateVersionId;
        public ItemCompositionMassAuthority massAuthority = ItemCompositionMassAuthority.AuthoredDefinition;
        public ItemCompositionMutationPurpose lastMutationPurpose = ItemCompositionMutationPurpose.RuntimeGameplay;
        public List<ItemMaterialEntryData> materials = new List<ItemMaterialEntryData>();
        public List<ItemComponentEntryData> components = new List<ItemComponentEntryData>();
        public string[] provenanceIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public List<ItemCompositionRevisionData> revisionHistory = new List<ItemCompositionRevisionData>();
        public long revision = 1L;

        public ItemCompositionRecordData Clone()
        {
            return new ItemCompositionRecordData
            {
                compositionId = compositionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                sourceItemDefinitionId = sourceItemDefinitionId ?? string.Empty,
                completeness = completeness,
                source = source ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                templateVersionId = templateVersionId ?? string.Empty,
                massAuthority = massAuthority,
                lastMutationPurpose = lastMutationPurpose,
                materials = materials == null ? new List<ItemMaterialEntryData>() : materials.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                components = components == null ? new List<ItemComponentEntryData>() : components.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                provenanceIds = CloneIds(provenanceIds),
                tags = CloneIds(tags),
                revisionHistory = revisionHistory == null ? new List<ItemCompositionRevisionData>() : revisionHistory.Select(entry => entry?.Clone()).Where(entry => entry != null).OrderBy(entry => entry.revision).ThenBy(entry => entry.operationId, StringComparer.Ordinal).ToList(),
                revision = revision
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ItemCompositionTemplateData
    {
        public string templateVersionId = "template.v1";
        public bool required;
        public ItemCompositionMassAuthority massAuthority = ItemCompositionMassAuthority.AuthoredDefinition;
        public ItemCompositionCompleteness completeness = ItemCompositionCompleteness.Unknown;
        public List<ItemMaterialEntryData> materials = new List<ItemMaterialEntryData>();
        public List<ItemComponentEntryData> components = new List<ItemComponentEntryData>();
        public string[] tags = Array.Empty<string>();

        public bool IsEmpty => (materials == null || materials.Count == 0) && (components == null || components.Count == 0);

        public ItemCompositionTemplateData Clone()
        {
            return new ItemCompositionTemplateData
            {
                completeness = completeness,
                required = required,
                massAuthority = massAuthority,
                templateVersionId = templateVersionId ?? string.Empty,
                materials = materials == null ? new List<ItemMaterialEntryData>() : materials.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                components = components == null ? new List<ItemComponentEntryData>() : components.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                tags = (tags ?? Array.Empty<string>()).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray()
            };
        }

        public ItemCompositionRecordData Instantiate(string itemInstanceId, string itemDefinitionId, string source = "item-definition.default-composition")
        {
            return new ItemCompositionRecordData
            {
                compositionId = $"item-composition.{itemInstanceId}",
                itemInstanceId = itemInstanceId ?? string.Empty,
                sourceItemDefinitionId = itemDefinitionId ?? string.Empty,
                completeness = completeness,
                source = source ?? string.Empty,
                templateVersionId = templateVersionId ?? string.Empty,
                massAuthority = massAuthority,
                lastMutationPurpose = ItemCompositionMutationPurpose.DefaultInitialization,
                materials = materials == null ? new List<ItemMaterialEntryData>() : materials.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                components = components == null ? new List<ItemComponentEntryData>() : components.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                tags = (tags ?? Array.Empty<string>()).Concat(new[] { "item.composition", "composition.default" }).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class ItemCompositionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ItemCompositionRecordData> records = new List<ItemCompositionRecordData>();

        public ItemCompositionRuntimeSaveData Clone()
        {
            return new ItemCompositionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<ItemCompositionRecordData>() : records.Select(record => record?.Clone()).Where(record => record != null).ToList()
            };
        }
    }

    public sealed class ItemCompositionSnapshot
    {
        public ItemCompositionSnapshot(ItemCompositionRecordData data)
        {
            Data = data?.Clone() ?? new ItemCompositionRecordData();
        }

        public ItemCompositionRecordData Data { get; }
        public string CompositionId => Data.compositionId ?? string.Empty;
        public string ItemInstanceId => Data.itemInstanceId ?? string.Empty;
        public string SourceItemDefinitionId => Data.sourceItemDefinitionId ?? string.Empty;
        public IReadOnlyList<ItemMaterialEntryData> Materials => Data.materials ?? new List<ItemMaterialEntryData>();
        public IReadOnlyList<ItemComponentEntryData> Components => Data.components ?? new List<ItemComponentEntryData>();
        public ItemCompositionCompleteness Completeness => Data.completeness;
        public long Revision => Data.revision;

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ItemCompositionInformationSubject.Create(ItemInstanceId, CompositionId, SourceItemDefinitionId, Data.tags);
        }
    }

    public sealed class ItemCompositionOperationResult
    {
        private ItemCompositionOperationResult(bool succeeded, bool preview, ItemCompositionOperationStatus status, string message, ItemCompositionSnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public ItemCompositionOperationStatus Status { get; }
        public string Message { get; }
        public ItemCompositionSnapshot Snapshot { get; }

        public static ItemCompositionOperationResult Success(ItemCompositionSnapshot snapshot, string message = "Item composition operation succeeded.", bool preview = false)
        {
            return new ItemCompositionOperationResult(true, preview, preview ? ItemCompositionOperationStatus.Preview : ItemCompositionOperationStatus.Succeeded, message, snapshot);
        }

        public static ItemCompositionOperationResult Failure(ItemCompositionOperationStatus status, string message)
        {
            return new ItemCompositionOperationResult(false, false, status, message, null);
        }
    }

    public sealed class ItemCompositionProjection
    {
        public ItemCompositionProjection(ItemCompositionSnapshot snapshot, bool denied, bool redacted, IReadOnlyList<ItemMaterialEntryData> visibleMaterials, IReadOnlyList<ItemComponentEntryData> visibleComponents, IReadOnlyList<string> redactedFields)
        {
            Snapshot = snapshot;
            Denied = denied;
            Redacted = redacted;
            VisibleMaterials = (visibleMaterials ?? Array.Empty<ItemMaterialEntryData>()).Select(entry => entry.Clone()).ToArray();
            VisibleComponents = (visibleComponents ?? Array.Empty<ItemComponentEntryData>()).Select(entry => entry.Clone()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public ItemCompositionSnapshot Snapshot { get; }
        public bool Denied { get; }
        public bool Redacted { get; }
        public IReadOnlyList<ItemMaterialEntryData> VisibleMaterials { get; }
        public IReadOnlyList<ItemComponentEntryData> VisibleComponents { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class DerivedItemMaterialProperties
    {
        public float KnownMassKg { get; set; }
        public float WeightedHardness { get; set; }
        public float WeightedDurability { get; set; }
        public float WeightedDurabilityPotential
        {
            get => WeightedDurability;
            set => WeightedDurability = value;
        }

        public float WeightedFlexibility { get; set; }
        public float WeightedConductivity { get; set; }
        public float WeightedFlammability { get; set; }
        public int MaterialCount { get; set; }
        public bool Incomplete { get; set; }
        public bool GameplayMassAuthoritative { get; set; }
        public ItemCompositionMassAuthority MassAuthority { get; set; } = ItemCompositionMassAuthority.AuthoredDefinition;
    }

    public sealed class MaterialCompatibilityEvaluation
    {
        public MaterialCompatibilityOutcome Outcome { get; set; } = MaterialCompatibilityOutcome.Neutral;
        public string RuleId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public float DurabilityMultiplier { get; set; } = 1f;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class CompositeMaterialExpansionEntry
    {
        public string MaterialDefinitionId { get; set; } = string.Empty;
        public float Ratio { get; set; }
        public float Purity { get; set; } = 1f;
    }

    public sealed class CompositeMaterialExpansionResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<CompositeMaterialExpansionEntry> Entries { get; set; } = Array.Empty<CompositeMaterialExpansionEntry>();
    }

    public sealed class ItemCompositionCreationRequest
    {
        public IInventoryItemDefinition Definition { get; set; }
        public ItemInstanceClassification Classification { get; set; } = ItemInstanceClassification.IndividuallyTracked;
        public string ItemInstanceId { get; set; } = string.Empty;
        public string CreatorPersonId { get; set; } = string.Empty;
        public string OwnerPersonId { get; set; } = string.Empty;
        public string CustodianPersonId { get; set; } = string.Empty;
        public string CreationSourceId { get; set; } = string.Empty;
        public bool RequireComposition { get; set; }
        public bool UseDefaultTemplate { get; set; } = true;
        public ItemCompositionRecordData ExplicitComposition { get; set; }
        public ItemCompositionMutationPurpose Purpose { get; set; } = ItemCompositionMutationPurpose.RuntimeGameplay;
        public bool Preview { get; set; }
    }

    public sealed class ItemCompositionCreationResult
    {
        private ItemCompositionCreationResult(bool succeeded, ItemCompositionOperationStatus status, string message, ItemInstanceSnapshot item, ItemCompositionSnapshot composition, bool preview)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Item = item;
            Composition = composition;
            Preview = preview;
        }

        public bool Succeeded { get; }
        public ItemCompositionOperationStatus Status { get; }
        public string Message { get; }
        public ItemInstanceSnapshot Item { get; }
        public ItemCompositionSnapshot Composition { get; }
        public bool Preview { get; }

        public static ItemCompositionCreationResult Success(ItemInstanceSnapshot item, ItemCompositionSnapshot composition, string message, bool preview = false)
        {
            return new ItemCompositionCreationResult(true, preview ? ItemCompositionOperationStatus.Preview : ItemCompositionOperationStatus.Succeeded, message, item, composition, preview);
        }

        public static ItemCompositionCreationResult Failure(ItemCompositionOperationStatus status, string message)
        {
            return new ItemCompositionCreationResult(false, status, message, null, null, false);
        }
    }
}
