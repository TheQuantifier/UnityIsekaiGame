using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Production
{
    [Serializable]
    public sealed class ProductionQuantityData
    {
        public string definitionId;
        public string itemInstanceId;
        public string sourceContainerId;
        public string locationId;
        public float quantity;
        public float sourceTotalQuantity;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public long expectedRuntimeRevision;
        public long expectedStackRevision;
        public string accessDecisionId;
        public bool perceived = true;
        public bool authoritative = true;
        public bool reusable;

        public ProductionQuantityData Clone()
        {
            return new ProductionQuantityData
            {
                definitionId = definitionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                sourceContainerId = sourceContainerId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                quantity = quantity,
                sourceTotalQuantity = sourceTotalQuantity,
                unit = unit,
                expectedRuntimeRevision = expectedRuntimeRevision,
                expectedStackRevision = expectedStackRevision,
                accessDecisionId = accessDecisionId ?? string.Empty,
                perceived = perceived,
                authoritative = authoritative,
                reusable = reusable
            };
        }
    }

    [Serializable]
    public sealed class ProductionToolCandidateData
    {
        public string itemInstanceId;
        public string toolDefinitionId;
        public ProductionToolRole role = ProductionToolRole.Unknown;
        public ProductionToolCategory category = ProductionToolCategory.Unknown;
        public string[] capabilityIds = Array.Empty<string>();
        public float quality = 1f;
        public float durability = 1f;
        public bool perceived = true;
        public bool authoritative = true;
        public string ownerPersonId;
        public long itemRevision;
        public long durabilityRevision;

        public ProductionToolCandidateData Clone()
        {
            return new ProductionToolCandidateData
            {
                itemInstanceId = itemInstanceId ?? string.Empty,
                toolDefinitionId = toolDefinitionId ?? string.Empty,
                role = role,
                category = category,
                capabilityIds = CloneIds(capabilityIds),
                quality = quality,
                durability = durability,
                perceived = perceived,
                authoritative = authoritative,
                ownerPersonId = ownerPersonId ?? string.Empty,
                itemRevision = itemRevision,
                durabilityRevision = durabilityRevision
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ProductionStationInstanceData
    {
        public string stationInstanceId;
        public string stationDefinitionId;
        public string locationId;
        public ProductionStationCategory category = ProductionStationCategory.Unknown;
        public string[] capabilityIds = Array.Empty<string>();
        public string ownerId;
        public bool perceived = true;
        public bool authoritative = true;
        public int reservationLimit = 1;
        public long revision = 1L;

        public ProductionStationInstanceData Clone()
        {
            return new ProductionStationInstanceData
            {
                stationInstanceId = stationInstanceId ?? string.Empty,
                stationDefinitionId = stationDefinitionId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                category = category,
                capabilityIds = CloneIds(capabilityIds),
                ownerId = ownerId ?? string.Empty,
                perceived = perceived,
                authoritative = authoritative,
                reservationLimit = Math.Max(1, reservationLimit),
                revision = Math.Max(1L, revision)
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ProductionContextData
    {
        [NonSerialized] public KnowledgeSnapshot knowledgeSnapshot;
        public string actorPersonId;
        public string actorBodyId;
        public string locationId;
        public string worldTime;
        public ProductionEvaluationPerspective perspective = ProductionEvaluationPerspective.Authoritative;
        public string[] capabilityIds = Array.Empty<string>();
        public string[] knownFactDefinitionIds = Array.Empty<string>();
        public string[] environmentKeys = Array.Empty<string>();
        public string[] accessKeys = Array.Empty<string>();
        public string[] bodyCapabilityIds = Array.Empty<string>();
        public List<ProductionToolCandidateData> toolCandidates = new List<ProductionToolCandidateData>();
        public List<ProductionQuantityData> resourceQuantities = new List<ProductionQuantityData>();
        public List<ProductionQuantityData> itemQuantities = new List<ProductionQuantityData>();
        public List<ProductionQuantityData> materialQuantities = new List<ProductionQuantityData>();

        public ProductionContextData Clone()
        {
            return new ProductionContextData
            {
                actorPersonId = actorPersonId ?? string.Empty,
                actorBodyId = actorBodyId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                perspective = perspective,
                knowledgeSnapshot = knowledgeSnapshot,
                capabilityIds = CloneIds(capabilityIds),
                knownFactDefinitionIds = CloneIds(knownFactDefinitionIds),
                environmentKeys = CloneIds(environmentKeys),
                accessKeys = CloneIds(accessKeys),
                bodyCapabilityIds = CloneIds(bodyCapabilityIds),
                toolCandidates = toolCandidates == null ? new List<ProductionToolCandidateData>() : toolCandidates.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                resourceQuantities = CloneQuantities(resourceQuantities),
                itemQuantities = CloneQuantities(itemQuantities),
                materialQuantities = CloneQuantities(materialQuantities)
            };
        }

        private static List<ProductionQuantityData> CloneQuantities(IEnumerable<ProductionQuantityData> values)
        {
            return (values ?? Array.Empty<ProductionQuantityData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToList();
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class ProductionInputAllocationData
    {
        public string allocationId;
        public string requirementId;
        public string requirementGroupId;
        public ProductionRequirementType requirementType = ProductionRequirementType.Unknown;
        public string definitionId;
        public string itemInstanceId;
        public string sourceContainerId;
        public string locationId;
        public float quantity;
        public float sourceTotalQuantity;
        public ProductionQuantityUnit unit = ProductionQuantityUnit.Count;
        public long expectedRuntimeRevision;
        public long expectedStackRevision;
        public string reservationId;
        public string accessDecisionId;
        public bool substitutionUsed;
        public bool reusable;
        public string message;

        public ProductionInputAllocationData Clone()
        {
            return new ProductionInputAllocationData
            {
                allocationId = allocationId ?? string.Empty,
                requirementId = requirementId ?? string.Empty,
                requirementGroupId = requirementGroupId ?? string.Empty,
                requirementType = requirementType,
                definitionId = definitionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                sourceContainerId = sourceContainerId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                quantity = quantity,
                sourceTotalQuantity = sourceTotalQuantity,
                unit = unit,
                expectedRuntimeRevision = expectedRuntimeRevision,
                expectedStackRevision = expectedStackRevision,
                reservationId = reservationId ?? string.Empty,
                accessDecisionId = accessDecisionId ?? string.Empty,
                substitutionUsed = substitutionUsed,
                reusable = reusable,
                message = message ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ProductionRequirementSelectionData
    {
        public string requirementId;
        public string requirementGroupId;
        public ProductionRequirementType requirementType = ProductionRequirementType.Unknown;
        public string selectedToolItemInstanceId;
        public string selectedToolDefinitionId;
        public string selectedStationInstanceId;
        public string selectedStationDefinitionId;
        public string selectedDefinitionId;
        public float quantity;
        public ProductionQuantityUnit quantityUnit = ProductionQuantityUnit.Count;
        public bool alternativeUsed;
        public float expectedToolWear;
        public string expectedWearChannel;
        public List<ProductionInputAllocationData> allocations = new List<ProductionInputAllocationData>();
        public string message;

        public ProductionRequirementSelectionData Clone()
        {
            return new ProductionRequirementSelectionData
            {
                requirementId = requirementId ?? string.Empty,
                requirementGroupId = requirementGroupId ?? string.Empty,
                requirementType = requirementType,
                selectedToolItemInstanceId = selectedToolItemInstanceId ?? string.Empty,
                selectedToolDefinitionId = selectedToolDefinitionId ?? string.Empty,
                selectedStationInstanceId = selectedStationInstanceId ?? string.Empty,
                selectedStationDefinitionId = selectedStationDefinitionId ?? string.Empty,
                selectedDefinitionId = selectedDefinitionId ?? string.Empty,
                quantity = quantity,
                quantityUnit = quantityUnit,
                alternativeUsed = alternativeUsed,
                expectedToolWear = expectedToolWear,
                expectedWearChannel = expectedWearChannel ?? string.Empty,
                allocations = allocations == null ? new List<ProductionInputAllocationData>() : allocations.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                message = message ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ProductionPlanDependencyData
    {
        public string dependencyId;
        public string dependencyType;
        public long revision;

        public ProductionPlanDependencyData Clone()
        {
            return new ProductionPlanDependencyData
            {
                dependencyId = dependencyId ?? string.Empty,
                dependencyType = dependencyType ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class ProductionRequirementPlanData
    {
        public string planId;
        public string productionJobId;
        public string actorPersonId;
        public string locationId;
        public ProductionPlanStatus status = ProductionPlanStatus.Planned;
        public string createdWorldTime;
        public string expiresWorldTime;
        public List<ProductionRequirementSelectionData> selections = new List<ProductionRequirementSelectionData>();
        public List<ProductionPlanDependencyData> dependencies = new List<ProductionPlanDependencyData>();
        public string signature;
        public long revision = 1L;

        public ProductionRequirementPlanData Clone()
        {
            return new ProductionRequirementPlanData
            {
                planId = planId ?? string.Empty,
                productionJobId = productionJobId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                status = status,
                createdWorldTime = createdWorldTime ?? string.Empty,
                expiresWorldTime = expiresWorldTime ?? string.Empty,
                selections = selections == null ? new List<ProductionRequirementSelectionData>() : selections.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                dependencies = dependencies == null ? new List<ProductionPlanDependencyData>() : dependencies.Select(entry => entry?.Clone()).Where(entry => entry != null).OrderBy(entry => entry.dependencyType, StringComparer.Ordinal).ThenBy(entry => entry.dependencyId, StringComparer.Ordinal).ToList(),
                signature = signature ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = planId ?? string.Empty,
                parentSubjectId = productionJobId ?? string.Empty,
                ownerPersonId = actorPersonId ?? string.Empty,
                tags = new[] { "domain.item", "item.production", "production.requirement-plan" }
            };
        }
    }

    [Serializable]
    public sealed class ProductionReservationData
    {
        public string reservationId;
        public string planId;
        public string productionJobId;
        public string actorPersonId;
        public string reservedRequirementId;
        public ProductionRequirementType reservedRequirementType = ProductionRequirementType.Unknown;
        public string reservedToolItemInstanceId;
        public string reservedStationInstanceId;
        public string reservedDefinitionId;
        public string reservedItemInstanceId;
        public string sourceContainerId;
        public string locationId;
        public float reservedQuantity;
        public ProductionQuantityUnit quantityUnit = ProductionQuantityUnit.Count;
        public string accessDecisionId;
        public string createdWorldTime;
        public string expiresWorldTime;
        public ProductionReservationStatus status = ProductionReservationStatus.Active;
        public long revision = 1L;

        public ProductionReservationData Clone()
        {
            return new ProductionReservationData
            {
                reservationId = reservationId ?? string.Empty,
                planId = planId ?? string.Empty,
                productionJobId = productionJobId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                reservedRequirementId = reservedRequirementId ?? string.Empty,
                reservedRequirementType = reservedRequirementType,
                reservedToolItemInstanceId = reservedToolItemInstanceId ?? string.Empty,
                reservedStationInstanceId = reservedStationInstanceId ?? string.Empty,
                reservedDefinitionId = reservedDefinitionId ?? string.Empty,
                reservedItemInstanceId = reservedItemInstanceId ?? string.Empty,
                sourceContainerId = sourceContainerId ?? string.Empty,
                locationId = locationId ?? string.Empty,
                reservedQuantity = reservedQuantity,
                quantityUnit = quantityUnit,
                accessDecisionId = accessDecisionId ?? string.Empty,
                createdWorldTime = createdWorldTime ?? string.Empty,
                expiresWorldTime = expiresWorldTime ?? string.Empty,
                status = status,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class ProductionRequirementRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ProductionStationInstanceData> stations = new List<ProductionStationInstanceData>();
        public List<ProductionRequirementPlanData> plans = new List<ProductionRequirementPlanData>();
        public List<ProductionReservationData> reservations = new List<ProductionReservationData>();

        public ProductionRequirementRuntimeSaveData Clone()
        {
            return new ProductionRequirementRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                stations = stations == null ? new List<ProductionStationInstanceData>() : stations.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                plans = plans == null ? new List<ProductionRequirementPlanData>() : plans.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList(),
                reservations = reservations == null ? new List<ProductionReservationData>() : reservations.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList()
            };
        }
    }

    public sealed class ProductionRequirementEvaluationResult
    {
        private ProductionRequirementEvaluationResult(bool succeeded, bool preview, ProductionRequirementEvaluationStatus status, string message, ProductionRequirementPlanData plan, IReadOnlyList<string> diagnostics)
        {
            Succeeded = succeeded;
            Preview = preview;
            Status = status;
            Message = message ?? string.Empty;
            Plan = plan?.Clone();
            Diagnostics = (diagnostics ?? Array.Empty<string>()).ToArray();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public ProductionRequirementEvaluationStatus Status { get; }
        public string Message { get; }
        public ProductionRequirementPlanData Plan { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static ProductionRequirementEvaluationResult Success(ProductionRequirementPlanData plan, string message = "Production requirements satisfied.", bool preview = false, IReadOnlyList<string> diagnostics = null)
        {
            return new ProductionRequirementEvaluationResult(true, preview, preview ? ProductionRequirementEvaluationStatus.Preview : ProductionRequirementEvaluationStatus.Succeeded, message, plan, diagnostics);
        }

        public static ProductionRequirementEvaluationResult Failure(ProductionRequirementEvaluationStatus status, string message, IReadOnlyList<string> diagnostics = null)
        {
            return new ProductionRequirementEvaluationResult(false, false, status, message, null, diagnostics);
        }
    }

    public sealed class ProductionReservationResult
    {
        private ProductionReservationResult(bool succeeded, ProductionRequirementEvaluationStatus status, string message, ProductionRequirementPlanData plan, IReadOnlyList<ProductionReservationData> reservations)
        {
            Succeeded = succeeded;
            Status = status;
            Message = message ?? string.Empty;
            Plan = plan?.Clone();
            Reservations = (reservations ?? Array.Empty<ProductionReservationData>()).Select(entry => entry.Clone()).ToArray();
        }

        public bool Succeeded { get; }
        public ProductionRequirementEvaluationStatus Status { get; }
        public string Message { get; }
        public ProductionRequirementPlanData Plan { get; }
        public IReadOnlyList<ProductionReservationData> Reservations { get; }

        public static ProductionReservationResult Success(ProductionRequirementPlanData plan, IReadOnlyList<ProductionReservationData> reservations, string message = "Production plan reserved.")
        {
            return new ProductionReservationResult(true, ProductionRequirementEvaluationStatus.Succeeded, message, plan, reservations);
        }

        public static ProductionReservationResult Failure(ProductionRequirementEvaluationStatus status, string message)
        {
            return new ProductionReservationResult(false, status, message, null, Array.Empty<ProductionReservationData>());
        }
    }
}
