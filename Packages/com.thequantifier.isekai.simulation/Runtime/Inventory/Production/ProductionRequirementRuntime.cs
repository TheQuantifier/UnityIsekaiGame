using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge;

namespace UnityIsekaiGame.Inventory.Production
{
    public sealed class ProductionRequirementRuntime
    {
        private readonly Dictionary<string, ProductionStationInstanceData> stationsById = new Dictionary<string, ProductionStationInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionRequirementPlanData> plansById = new Dictionary<string, ProductionRequirementPlanData>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionReservationData> reservationsById = new Dictionary<string, ProductionReservationData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int StationCount => stationsById.Count;
        public int PlanCount => plansById.Count;
        public int ReservationCount => reservationsById.Count;

        public IReadOnlyList<ProductionStationInstanceData> Stations => stationsById.Values.OrderBy(entry => entry.stationInstanceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionRequirementPlanData> Plans => plansById.Values.OrderBy(entry => entry.planId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();
        public IReadOnlyList<ProductionReservationData> Reservations => reservationsById.Values.OrderBy(entry => entry.reservationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToArray();

        public ProductionRequirementEvaluationResult RegisterStation(ProductionStationDefinition definition, string stationInstanceId, string locationId, string ownerId = "", IEnumerable<string> extraCapabilities = null, bool perceived = true, bool authoritative = true)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.MissingStation, "Cannot register a station without a station definition.");
            }

            if (string.IsNullOrWhiteSpace(stationInstanceId))
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.ValidationFailed, "Station instance ID is required.");
            }

            ProductionStationInstanceData existing = stationsById.TryGetValue(stationInstanceId, out ProductionStationInstanceData current) ? current : null;
            ProductionStationInstanceData station = new ProductionStationInstanceData
            {
                stationInstanceId = stationInstanceId,
                stationDefinitionId = definition.Id,
                locationId = locationId ?? string.Empty,
                category = definition.Category,
                capabilityIds = definition.CapabilityIds.Concat(extraCapabilities ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ownerId = ownerId ?? string.Empty,
                perceived = perceived,
                authoritative = authoritative,
                reservationLimit = definition.ConcurrentReservationLimit,
                revision = existing == null ? 1L : existing.revision + 1L
            };

            stationsById[station.stationInstanceId] = station;
            revision++;
            return ProductionRequirementEvaluationResult.Success(new ProductionRequirementPlanData { planId = $"station-registration.{station.stationInstanceId}", revision = station.revision }, "Production station registered.");
        }

        public bool TryGetStation(string stationInstanceId, out ProductionStationInstanceData station)
        {
            if (!string.IsNullOrWhiteSpace(stationInstanceId) && stationsById.TryGetValue(stationInstanceId, out ProductionStationInstanceData found))
            {
                station = found.Clone();
                return true;
            }

            station = null;
            return false;
        }

        public ProductionRequirementEvaluationResult EvaluateRequirements(
            IReadOnlyList<ProductionRequirementDefinition> requirements,
            ProductionContextData context,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime = null,
            ItemDurabilityRuntime durabilityRuntime = null,
            string productionJobId = "",
            bool preview = false,
            string planId = "")
        {
            ProductionContextData workingContext = context?.Clone() ?? new ProductionContextData();
            List<string> diagnostics = new List<string>();
            if (requirements == null || requirements.Count == 0)
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.MissingRequirement, "No production requirements were supplied.");
            }

            string resolvedJobId = string.IsNullOrWhiteSpace(productionJobId) ? "production-job.unassigned" : productionJobId;
            ProductionRequirementPlanData plan = new ProductionRequirementPlanData
            {
                planId = string.IsNullOrWhiteSpace(planId) ? StableId("production-plan", resolvedJobId, string.Join("|", requirements.Select(requirement => requirement == null ? "null" : requirement.Id))) : planId,
                productionJobId = resolvedJobId,
                actorPersonId = workingContext.actorPersonId ?? string.Empty,
                locationId = workingContext.locationId ?? string.Empty,
                status = ProductionPlanStatus.Planned,
                createdWorldTime = workingContext.worldTime ?? string.Empty,
                expiresWorldTime = string.Empty,
                revision = 1L
            };

            foreach (ProductionRequirementDefinition requirement in requirements.Where(requirement => requirement != null).OrderBy(requirement => requirement.Priority).ThenBy(requirement => requirement.Id, StringComparer.Ordinal))
            {
                if (!TryResolveRequirement(requirement, workingContext, registry, itemRuntime, durabilityRuntime, out ProductionRequirementSelectionData selection, out ProductionRequirementEvaluationStatus failureStatus, out string failure))
                {
                    diagnostics.Add($"{requirement.Id}: {failure}");
                    if (requirement.Strictness == ProductionRequirementStrictness.Required)
                    {
                        return ProductionRequirementEvaluationResult.Failure(failureStatus, failure, diagnostics);
                    }

                    continue;
                }

                plan.selections.Add(selection);
                AddDependencies(plan, selection, itemRuntime, durabilityRuntime);
            }

            plan.dependencies = plan.dependencies
                .GroupBy(entry => $"{entry.dependencyType}:{entry.dependencyId}", StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(entry => entry.revision).First().Clone())
                .OrderBy(entry => entry.dependencyType, StringComparer.Ordinal)
                .ThenBy(entry => entry.dependencyId, StringComparer.Ordinal)
                .ToList();
            plan.signature = ComputeSignature(plan);

            if (preview)
            {
                return ProductionRequirementEvaluationResult.Success(plan, "Production requirement preview prepared.", preview: true, diagnostics: diagnostics);
            }

            bool replacing = plansById.TryGetValue(plan.planId, out ProductionRequirementPlanData existing);
            plan.revision = replacing ? existing.revision + 1L : 1L;
            plansById[plan.planId] = plan.Clone();
            revision++;
            return ProductionRequirementEvaluationResult.Success(plan, "Production requirements satisfied.", diagnostics: diagnostics);
        }

        public ProductionReservationResult ReservePlan(string planId, string expiresWorldTime = "")
        {
            if (string.IsNullOrWhiteSpace(planId) || !plansById.TryGetValue(planId, out ProductionRequirementPlanData plan))
            {
                return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.MissingRequirement, $"Production plan '{planId}' was not found.");
            }

            if (plan.status == ProductionPlanStatus.Invalidated)
            {
                return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.StalePlan, $"Production plan '{planId}' is invalidated.");
            }

            ExpireReservations(expiresWorldTime);
            List<ProductionReservationData> created = new List<ProductionReservationData>();
            foreach (ProductionRequirementSelectionData selection in plan.selections.OrderBy(selection => selection.requirementId, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(selection.selectedToolItemInstanceId) && HasActiveReservationForTool(selection.selectedToolItemInstanceId, plan.planId))
                {
                    return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.Conflict, $"Tool item '{selection.selectedToolItemInstanceId}' is already reserved.");
                }

                foreach (ProductionInputAllocationData allocation in selection.allocations ?? new List<ProductionInputAllocationData>())
                {
                    if (!AllocationHasReservationCapacity(allocation, plan.planId))
                    {
                        return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.Conflict, $"Input allocation '{allocation.allocationId}' has no remaining reservation capacity.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(selection.selectedStationInstanceId) && !StationHasCapacity(selection.selectedStationInstanceId, plan.planId))
                {
                    return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.Conflict, $"Station '{selection.selectedStationInstanceId}' has no reservation capacity.");
                }
            }

            foreach (ProductionRequirementSelectionData selection in plan.selections.OrderBy(selection => selection.requirementId, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(selection.selectedToolItemInstanceId) || !string.IsNullOrWhiteSpace(selection.selectedStationInstanceId))
                {
                    string reservationId = StableId("production-reservation", plan.planId, selection.requirementId, selection.selectedToolItemInstanceId, selection.selectedStationInstanceId);
                    ProductionReservationData reservation = new ProductionReservationData
                    {
                        reservationId = reservationId,
                        planId = plan.planId,
                        productionJobId = plan.productionJobId,
                        actorPersonId = plan.actorPersonId,
                        reservedRequirementId = selection.requirementId,
                        reservedRequirementType = selection.requirementType,
                        reservedToolItemInstanceId = selection.selectedToolItemInstanceId ?? string.Empty,
                        reservedStationInstanceId = selection.selectedStationInstanceId ?? string.Empty,
                        createdWorldTime = plan.createdWorldTime ?? string.Empty,
                        expiresWorldTime = expiresWorldTime ?? string.Empty,
                        status = ProductionReservationStatus.Active,
                        revision = reservationsById.TryGetValue(reservationId, out ProductionReservationData existing) ? existing.revision + 1L : 1L
                    };
                    reservationsById[reservation.reservationId] = reservation;
                    created.Add(reservation.Clone());
                }

                foreach (ProductionInputAllocationData allocation in selection.allocations ?? new List<ProductionInputAllocationData>())
                {
                    string reservationId = StableId("production-reservation", plan.planId, allocation.allocationId);
                    allocation.reservationId = reservationId;
                    ProductionReservationData reservation = new ProductionReservationData
                    {
                        reservationId = reservationId,
                        planId = plan.planId,
                        productionJobId = plan.productionJobId,
                        actorPersonId = plan.actorPersonId,
                        reservedRequirementId = allocation.requirementId,
                        reservedRequirementType = allocation.requirementType,
                        reservedDefinitionId = allocation.definitionId ?? string.Empty,
                        reservedItemInstanceId = allocation.itemInstanceId ?? string.Empty,
                        sourceContainerId = allocation.sourceContainerId ?? string.Empty,
                        locationId = allocation.locationId ?? string.Empty,
                        reservedQuantity = allocation.quantity,
                        quantityUnit = allocation.unit,
                        accessDecisionId = allocation.accessDecisionId ?? string.Empty,
                        createdWorldTime = plan.createdWorldTime ?? string.Empty,
                        expiresWorldTime = expiresWorldTime ?? string.Empty,
                        status = ProductionReservationStatus.Active,
                        revision = reservationsById.TryGetValue(reservationId, out ProductionReservationData existing) ? existing.revision + 1L : 1L
                    };
                    reservationsById[reservation.reservationId] = reservation;
                    created.Add(reservation.Clone());
                }
            }

            plan.status = ProductionPlanStatus.Reserved;
            plan.expiresWorldTime = expiresWorldTime ?? string.Empty;
            plan.revision++;
            plansById[plan.planId] = plan.Clone();
            revision++;
            return ProductionReservationResult.Success(plan, created);
        }

        public ProductionReservationResult ReleasePlanReservations(string planId)
        {
            if (string.IsNullOrWhiteSpace(planId) || !plansById.TryGetValue(planId, out ProductionRequirementPlanData plan))
            {
                return ProductionReservationResult.Failure(ProductionRequirementEvaluationStatus.MissingRequirement, $"Production plan '{planId}' was not found.");
            }

            List<ProductionReservationData> released = new List<ProductionReservationData>();
            foreach (ProductionReservationData reservation in reservationsById.Values.Where(reservation => reservation.status == ProductionReservationStatus.Active && string.Equals(reservation.planId, planId, StringComparison.Ordinal)).ToArray())
            {
                reservation.status = ProductionReservationStatus.Released;
                reservation.revision++;
                released.Add(reservation.Clone());
            }

            plan.status = ProductionPlanStatus.Released;
            plan.revision++;
            plansById[planId] = plan.Clone();
            revision++;
            return ProductionReservationResult.Success(plan, released, "Production plan reservations released.");
        }

        public ProductionRequirementEvaluationResult ValidatePlanCurrent(string planId, ItemInstanceIdentityRuntime itemRuntime = null, ItemDurabilityRuntime durabilityRuntime = null)
        {
            if (string.IsNullOrWhiteSpace(planId) || !plansById.TryGetValue(planId, out ProductionRequirementPlanData plan))
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.MissingRequirement, $"Production plan '{planId}' was not found.");
            }

            foreach (ProductionPlanDependencyData dependency in plan.dependencies ?? new List<ProductionPlanDependencyData>())
            {
                long current = CurrentDependencyRevision(dependency, itemRuntime, durabilityRuntime);
                if (current != dependency.revision)
                {
                    ProductionRequirementPlanData invalidated = plan.Clone();
                    invalidated.status = ProductionPlanStatus.Invalidated;
                    invalidated.revision++;
                    plansById[invalidated.planId] = invalidated.Clone();
                    revision++;
                    return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.StalePlan, $"Production plan '{planId}' dependency '{dependency.dependencyType}:{dependency.dependencyId}' changed from revision {dependency.revision} to {current}.");
                }
            }

            return ProductionRequirementEvaluationResult.Success(plan.Clone(), "Production plan dependencies are current.");
        }

        public ProductionRequirementEvaluationResult ApplyToolWearForPlan(
            string planId,
            ItemInstanceIdentityRuntime itemRuntime,
            UnityIsekaiGame.Inventory.Composition.ItemCompositionRuntime compositionRuntime,
            UnityIsekaiGame.Inventory.Quality.ItemQualityAffixRuntime qualityRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            DefinitionRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(planId) || !plansById.TryGetValue(planId, out ProductionRequirementPlanData plan))
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.MissingRequirement, $"Production plan '{planId}' was not found.");
            }

            return ProductionRequirementEvaluationResult.Success(plan.Clone(), "Production planning exposes expected tool wear only; execution coordinators apply durability mutation after production commits.");
        }

        public ProductionRequirementRuntimeSaveData CreateSaveData()
        {
            return new ProductionRequirementRuntimeSaveData
            {
                schemaVersion = ProductionRequirementRuntimeSaveData.CurrentSchemaVersion,
                revision = revision,
                stations = stationsById.Values.OrderBy(entry => entry.stationInstanceId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                plans = plansById.Values.OrderBy(entry => entry.planId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                reservations = reservationsById.Values.OrderBy(entry => entry.reservationId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList()
            };
        }

        public ProductionRequirementEvaluationResult RestoreFromSaveData(ProductionRequirementRuntimeSaveData saveData)
        {
            if (!ValidateSaveData(saveData, out string failure))
            {
                return ProductionRequirementEvaluationResult.Failure(ProductionRequirementEvaluationStatus.RestoreFailed, failure);
            }

            stationsById.Clear();
            plansById.Clear();
            reservationsById.Clear();
            foreach (ProductionStationInstanceData station in saveData.stations.Select(entry => entry.Clone()).OrderBy(entry => entry.stationInstanceId, StringComparer.Ordinal))
            {
                stationsById[station.stationInstanceId] = station;
            }

            foreach (ProductionRequirementPlanData plan in saveData.plans.Select(entry => entry.Clone()).OrderBy(entry => entry.planId, StringComparer.Ordinal))
            {
                plansById[plan.planId] = plan;
            }

            foreach (ProductionReservationData reservation in saveData.reservations.Select(entry => entry.Clone()).OrderBy(entry => entry.reservationId, StringComparer.Ordinal))
            {
                reservationsById[reservation.reservationId] = reservation;
            }

            revision = Math.Max(0L, saveData.revision);
            return ProductionRequirementEvaluationResult.Success(null, "Production requirement runtime restored.");
        }

        public static bool ValidateSaveData(ProductionRequirementRuntimeSaveData saveData, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Production requirement save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != ProductionRequirementRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported production requirement schema version {saveData.schemaVersion}.";
                return false;
            }

            if (saveData.revision < 0L)
            {
                failure = "Production requirement runtime revision cannot be negative.";
                return false;
            }

            HashSet<string> stationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionStationInstanceData station in saveData.stations ?? new List<ProductionStationInstanceData>())
            {
                if (station == null || string.IsNullOrWhiteSpace(station.stationInstanceId) || string.IsNullOrWhiteSpace(station.stationDefinitionId))
                {
                    failure = "Production station save entry is missing an instance or definition ID.";
                    return false;
                }

                if (!stationIds.Add(station.stationInstanceId))
                {
                    failure = $"Duplicate production station instance '{station.stationInstanceId}'.";
                    return false;
                }
            }

            HashSet<string> planIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionRequirementPlanData plan in saveData.plans ?? new List<ProductionRequirementPlanData>())
            {
                if (plan == null || string.IsNullOrWhiteSpace(plan.planId))
                {
                    failure = "Production plan save entry is missing an ID.";
                    return false;
                }

                if (!planIds.Add(plan.planId))
                {
                    failure = $"Duplicate production plan '{plan.planId}'.";
                    return false;
                }
            }

            HashSet<string> reservationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionReservationData reservation in saveData.reservations ?? new List<ProductionReservationData>())
            {
                if (reservation == null || string.IsNullOrWhiteSpace(reservation.reservationId) || string.IsNullOrWhiteSpace(reservation.planId))
                {
                    failure = "Production reservation save entry is missing an ID or plan reference.";
                    return false;
                }

                if (!reservationIds.Add(reservation.reservationId))
                {
                    failure = $"Duplicate production reservation '{reservation.reservationId}'.";
                    return false;
                }

                if (!planIds.Contains(reservation.planId))
                {
                    failure = $"Production reservation '{reservation.reservationId}' references missing plan '{reservation.planId}'.";
                    return false;
                }
            }

            return true;
        }

        private bool TryResolveRequirement(
            ProductionRequirementDefinition requirement,
            ProductionContextData context,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            out ProductionRequirementSelectionData selection,
            out ProductionRequirementEvaluationStatus failureStatus,
            out string failure)
        {
            RequirementSpec primary = RequirementSpec.From(requirement);
            if (TryResolveSpec(requirement, primary, context, registry, itemRuntime, durabilityRuntime, alternativeUsed: false, out selection, out failureStatus, out failure))
            {
                return true;
            }

            foreach (ProductionRequirementAlternativeDefinition alternative in requirement.Alternatives.OrderBy(entry => entry.RequirementType).ThenBy(entry => entry.ToolDefinitionId, StringComparer.Ordinal).ThenBy(entry => entry.StationDefinitionId, StringComparer.Ordinal))
            {
                RequirementSpec spec = RequirementSpec.From(requirement, alternative);
                if (TryResolveSpec(requirement, spec, context, registry, itemRuntime, durabilityRuntime, alternativeUsed: true, out selection, out failureStatus, out failure))
                {
                    return true;
                }
            }

            selection = null;
            return false;
        }

        private bool TryResolveSpec(
            ProductionRequirementDefinition requirement,
            RequirementSpec spec,
            ProductionContextData context,
            DefinitionRegistry registry,
            ItemInstanceIdentityRuntime itemRuntime,
            ItemDurabilityRuntime durabilityRuntime,
            bool alternativeUsed,
            out ProductionRequirementSelectionData selection,
            out ProductionRequirementEvaluationStatus failureStatus,
            out string failure)
        {
            selection = BaseSelection(requirement, spec, alternativeUsed);
            failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
            failure = string.Empty;

            switch (spec.Type)
            {
                case ProductionRequirementType.Tool:
                    ProductionToolCandidateData tool = SelectToolCandidate(spec, context, registry, itemRuntime, durabilityRuntime);
                    if (tool == null)
                    {
                        failureStatus = ProductionRequirementEvaluationStatus.MissingTool;
                        failure = $"No available tool satisfies requirement '{requirement.Id}'.";
                        return false;
                    }

                    selection.selectedToolItemInstanceId = tool.itemInstanceId;
                    selection.selectedToolDefinitionId = tool.toolDefinitionId;
                    selection.expectedToolWear = spec.ToolDefinition == null ? 0f : spec.ToolDefinition.DurabilityWearPerUse;
                    selection.expectedWearChannel = spec.ToolCategory == ProductionToolCategory.Hammering ? "impact" : spec.ToolCategory.ToString();
                    selection.message = "Tool selected.";
                    return true;
                case ProductionRequirementType.Station:
                    ProductionStationInstanceData station = SelectStation(spec, context, registry);
                    if (station == null)
                    {
                        failureStatus = ProductionRequirementEvaluationStatus.MissingStation;
                        failure = $"No available station satisfies requirement '{requirement.Id}'.";
                        return false;
                    }

                    selection.selectedStationInstanceId = station.stationInstanceId;
                    selection.selectedStationDefinitionId = station.stationDefinitionId;
                    selection.message = "Station selected.";
                    return true;
                case ProductionRequirementType.SkillCapability:
                    return ResolveSetMembership(context.capabilityIds, spec.CapabilityId, ProductionRequirementEvaluationStatus.MissingCapability, $"Required skill or capability '{spec.CapabilityId}' is missing.", out failureStatus, out failure);
                case ProductionRequirementType.Knowledge:
                    return ResolveKnowledge(context, spec.KnowledgeFactDefinitionId, selection, out failureStatus, out failure);
                case ProductionRequirementType.Resource:
                    return ResolveQuantity(context.resourceQuantities, context, spec.ResourceId, spec.Quantity, ProductionRequirementEvaluationStatus.MissingResource, selection, alternativeUsed, out failureStatus, out failure);
                case ProductionRequirementType.Item:
                    return ResolveQuantity(context.itemQuantities, context, spec.ItemDefinitionId, spec.Quantity, ProductionRequirementEvaluationStatus.MissingItem, selection, alternativeUsed, out failureStatus, out failure);
                case ProductionRequirementType.Material:
                    return ResolveQuantity(context.materialQuantities, context, spec.MaterialDefinitionId, spec.Quantity, ProductionRequirementEvaluationStatus.MissingMaterial, selection, alternativeUsed, out failureStatus, out failure);
                case ProductionRequirementType.Environment:
                    return ResolveSetMembership(context.environmentKeys, spec.EnvironmentKey, ProductionRequirementEvaluationStatus.MissingRequirement, $"Required environment '{spec.EnvironmentKey}' is missing.", out failureStatus, out failure);
                case ProductionRequirementType.Access:
                    return ResolveSetMembership(context.accessKeys, spec.AccessKey, ProductionRequirementEvaluationStatus.AccessDenied, $"Required access '{spec.AccessKey}' is missing.", out failureStatus, out failure);
                case ProductionRequirementType.Body:
                    return ResolveSetMembership(context.bodyCapabilityIds, spec.BodyCapabilityId, ProductionRequirementEvaluationStatus.MissingCapability, $"Required body capability '{spec.BodyCapabilityId}' is missing.", out failureStatus, out failure);
                default:
                    failureStatus = ProductionRequirementEvaluationStatus.ValidationFailed;
                    failure = $"Requirement '{requirement.Id}' has unsupported type '{spec.Type}'.";
                    return false;
            }
        }

        private ProductionToolCandidateData SelectToolCandidate(RequirementSpec spec, ProductionContextData context, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemDurabilityRuntime durabilityRuntime)
        {
            return (context.toolCandidates ?? new List<ProductionToolCandidateData>())
                .Where(candidate => CandidateMatchesTool(candidate, spec, context.perspective, registry, itemRuntime, durabilityRuntime))
                .OrderByDescending(candidate => registry != null && registry.TryGet(candidate.toolDefinitionId, out ProductionToolDefinition tool) ? tool.Priority : 0)
                .ThenByDescending(candidate => candidate.quality)
                .ThenByDescending(candidate => candidate.durability)
                .ThenBy(candidate => candidate.toolDefinitionId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.itemInstanceId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?.Clone();
        }

        private bool CandidateMatchesTool(ProductionToolCandidateData candidate, RequirementSpec spec, ProductionEvaluationPerspective perspective, DefinitionRegistry registry, ItemInstanceIdentityRuntime itemRuntime, ItemDurabilityRuntime durabilityRuntime)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.itemInstanceId) || string.IsNullOrWhiteSpace(candidate.toolDefinitionId))
            {
                return false;
            }

            if (perspective == ProductionEvaluationPerspective.Perceived && !candidate.perceived)
            {
                return false;
            }

            if (perspective == ProductionEvaluationPerspective.Authoritative && !candidate.authoritative)
            {
                return false;
            }

            if (HasActiveReservationForTool(candidate.itemInstanceId, string.Empty))
            {
                return false;
            }

            ProductionToolDefinition tool = null;
            registry?.TryGet(candidate.toolDefinitionId, out tool);
            bool supports = tool != null
                ? tool.Supports(spec.ToolRole, spec.ToolCategory, spec.ToolCapabilityId, spec.ToolDefinitionId, spec.AllowSubstitution)
                : CandidateSupports(candidate, spec);
            if (!supports)
            {
                return false;
            }

            if (candidate.quality < (tool?.MinimumQuality ?? 0f) || candidate.durability < (tool?.MinimumDurability ?? 0f))
            {
                return false;
            }

            if (itemRuntime != null && !itemRuntime.TryGetSnapshot(candidate.itemInstanceId, out ItemInstanceSnapshot item))
            {
                return false;
            }

            if (perspective == ProductionEvaluationPerspective.Authoritative && durabilityRuntime != null && durabilityRuntime.TryGetDurabilityForItem(candidate.itemInstanceId, out ItemDurabilitySnapshot durability))
            {
                return durability.NormalizedDurability >= (tool?.MinimumDurability ?? 0f);
            }

            return true;
        }

        private static bool CandidateSupports(ProductionToolCandidateData candidate, RequirementSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.ToolDefinitionId)
                && !string.Equals(candidate.toolDefinitionId, spec.ToolDefinitionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (spec.ToolRole != ProductionToolRole.Unknown && candidate.role != spec.ToolRole)
            {
                return false;
            }

            if (spec.ToolCategory != ProductionToolCategory.Unknown && candidate.category != spec.ToolCategory)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(spec.ToolCapabilityId) || (candidate.capabilityIds ?? Array.Empty<string>()).Contains(spec.ToolCapabilityId, StringComparer.Ordinal);
        }

        private ProductionStationInstanceData SelectStation(RequirementSpec spec, ProductionContextData context, DefinitionRegistry registry)
        {
            return stationsById.Values
                .Where(station => StationMatches(station, spec, context, registry))
                .OrderByDescending(station => registry != null && registry.TryGet(station.stationDefinitionId, out ProductionStationDefinition definition) ? definition.Priority : 0)
                .ThenBy(station => station.stationDefinitionId, StringComparer.Ordinal)
                .ThenBy(station => station.stationInstanceId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?.Clone();
        }

        private bool StationMatches(ProductionStationInstanceData station, RequirementSpec spec, ProductionContextData context, DefinitionRegistry registry)
        {
            if (station == null || string.IsNullOrWhiteSpace(station.stationInstanceId))
            {
                return false;
            }

            if (context.perspective == ProductionEvaluationPerspective.Perceived && !station.perceived)
            {
                return false;
            }

            if (context.perspective == ProductionEvaluationPerspective.Authoritative && !station.authoritative)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.locationId) && !string.IsNullOrWhiteSpace(station.locationId) && !string.Equals(station.locationId, context.locationId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!StationHasCapacity(station.stationInstanceId, string.Empty))
            {
                return false;
            }

            ProductionStationDefinition definition = null;
            registry?.TryGet(station.stationDefinitionId, out definition);
            if (!string.IsNullOrWhiteSpace(spec.StationDefinitionId) && !string.Equals(station.stationDefinitionId, spec.StationDefinitionId, StringComparison.Ordinal))
            {
                return false;
            }

            bool supports = definition != null
                ? definition.Supports(spec.StationCategory, spec.StationCapabilityId, spec.ToolRole)
                : (spec.StationCategory == ProductionStationCategory.Unknown || station.category == spec.StationCategory)
                    && (string.IsNullOrWhiteSpace(spec.StationCapabilityId) || (station.capabilityIds ?? Array.Empty<string>()).Contains(spec.StationCapabilityId, StringComparer.Ordinal));
            return supports;
        }

        private static bool ResolveSetMembership(IEnumerable<string> values, string required, ProductionRequirementEvaluationStatus status, string message, out ProductionRequirementEvaluationStatus failureStatus, out string failure)
        {
            if (string.IsNullOrWhiteSpace(required) || (values ?? Array.Empty<string>()).Contains(required, StringComparer.Ordinal))
            {
                failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
                failure = string.Empty;
                return true;
            }

            failureStatus = status;
            failure = message;
            return false;
        }

        private static bool ResolveKnowledge(ProductionContextData context, string required, ProductionRequirementSelectionData selection, out ProductionRequirementEvaluationStatus failureStatus, out string failure)
        {
            if (string.IsNullOrWhiteSpace(required))
            {
                failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
                failure = string.Empty;
                return true;
            }

            KnowledgeSnapshot snapshot = context?.knowledgeSnapshot;
            KnowledgeBeliefRecord belief = snapshot?.KnownFacts.FirstOrDefault(record => string.Equals(record.Proposition.FactDefinitionId, required, StringComparison.Ordinal));
            if (belief != null)
            {
                selection.selectedDefinitionId = required;
                selection.allocations.Add(new ProductionInputAllocationData
                {
                    allocationId = StableId("production-knowledge-allocation", selection.requirementId, required, snapshot.PersonId, belief.BeliefId),
                    requirementId = selection.requirementId,
                    requirementGroupId = selection.requirementGroupId,
                    requirementType = selection.requirementType,
                    definitionId = required,
                    quantity = 1f,
                    sourceTotalQuantity = 1f,
                    unit = ProductionQuantityUnit.Count,
                    expectedRuntimeRevision = snapshot.Revision,
                    accessDecisionId = belief.BeliefId,
                    message = "Knowledge resolved from Person Knowledge snapshot."
                });
                failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
                failure = string.Empty;
                return true;
            }

            if ((context?.knownFactDefinitionIds ?? Array.Empty<string>()).Contains(required, StringComparer.Ordinal))
            {
                selection.selectedDefinitionId = required;
                failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
                failure = string.Empty;
                return true;
            }

            failureStatus = ProductionRequirementEvaluationStatus.MissingKnowledge;
            failure = $"Required knowledge '{required}' is missing.";
            return false;
        }

        private bool ResolveQuantity(IEnumerable<ProductionQuantityData> quantities, ProductionContextData context, string definitionId, float required, ProductionRequirementEvaluationStatus status, ProductionRequirementSelectionData selection, bool substitutionUsed, out ProductionRequirementEvaluationStatus failureStatus, out string failure)
        {
            List<ProductionInputAllocationData> allocations = BuildQuantityAllocations(quantities, context?.perspective ?? ProductionEvaluationPerspective.Authoritative, definitionId, required, selection, substitutionUsed);
            float available = allocations.Sum(entry => entry.quantity);
            if (!string.IsNullOrWhiteSpace(definitionId) && available >= required)
            {
                selection.selectedDefinitionId = definitionId;
                selection.quantity = required;
                selection.allocations.AddRange(allocations);
                failureStatus = ProductionRequirementEvaluationStatus.Succeeded;
                failure = string.Empty;
                return true;
            }

            failureStatus = status;
            failure = $"Required quantity {required:0.###} of '{definitionId}' is not available; available={available:0.###}.";
            return false;
        }

        private List<ProductionInputAllocationData> BuildQuantityAllocations(IEnumerable<ProductionQuantityData> quantities, ProductionEvaluationPerspective perspective, string definitionId, float required, ProductionRequirementSelectionData selection, bool substitutionUsed)
        {
            List<ProductionInputAllocationData> allocations = new List<ProductionInputAllocationData>();
            float remaining = required;
            foreach (ProductionQuantityData source in (quantities ?? Array.Empty<ProductionQuantityData>())
                .Where(entry => QuantitySourceMatches(entry, perspective, definitionId))
                .OrderBy(entry => string.IsNullOrWhiteSpace(entry.itemInstanceId) ? 1 : 0)
                .ThenBy(entry => entry.itemInstanceId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(entry => entry.sourceContainerId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(entry => entry.locationId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(entry => entry.definitionId ?? string.Empty, StringComparer.Ordinal))
            {
                if (remaining <= 0f)
                {
                    break;
                }

                float reserved = ReservedQuantityFor(source, selection.requirementType);
                float available = Math.Max(0f, source.quantity - reserved);
                if (available <= 0f)
                {
                    continue;
                }

                float allocated = Math.Min(remaining, available);
                allocations.Add(new ProductionInputAllocationData
                {
                    allocationId = StableId("production-input-allocation", selection.requirementId, QuantitySourceKey(source), allocated.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)),
                    requirementId = selection.requirementId,
                    requirementGroupId = selection.requirementGroupId,
                    requirementType = selection.requirementType,
                    definitionId = source.definitionId ?? string.Empty,
                    itemInstanceId = source.itemInstanceId ?? string.Empty,
                    sourceContainerId = source.sourceContainerId ?? string.Empty,
                    locationId = source.locationId ?? string.Empty,
                    quantity = allocated,
                    sourceTotalQuantity = source.quantity,
                    unit = source.unit,
                    expectedRuntimeRevision = source.expectedRuntimeRevision,
                    expectedStackRevision = source.expectedStackRevision,
                    accessDecisionId = source.accessDecisionId ?? string.Empty,
                    substitutionUsed = substitutionUsed,
                    reusable = source.reusable,
                    message = "Quantity source allocated."
                });
                remaining -= allocated;
            }

            return allocations;
        }

        private static bool QuantitySourceMatches(ProductionQuantityData entry, ProductionEvaluationPerspective perspective, string definitionId)
        {
            if (entry == null || string.IsNullOrWhiteSpace(definitionId) || !string.Equals(entry.definitionId, definitionId, StringComparison.Ordinal) || entry.quantity <= 0f)
            {
                return false;
            }

            return perspective == ProductionEvaluationPerspective.Perceived ? entry.perceived : entry.authoritative;
        }

        private static ProductionRequirementSelectionData BaseSelection(ProductionRequirementDefinition requirement, RequirementSpec spec, bool alternativeUsed)
        {
            return new ProductionRequirementSelectionData
            {
                requirementId = requirement.Id,
                requirementGroupId = requirement.RequirementGroupId,
                requirementType = spec.Type,
                selectedDefinitionId = FirstNonEmpty(spec.ResourceId, spec.ItemDefinitionId, spec.MaterialDefinitionId, spec.CapabilityId, spec.KnowledgeFactDefinitionId, spec.EnvironmentKey, spec.AccessKey, spec.BodyCapabilityId),
                quantity = spec.Quantity,
                quantityUnit = spec.QuantityUnit,
                alternativeUsed = alternativeUsed
            };
        }

        private void AddDependencies(ProductionRequirementPlanData plan, ProductionRequirementSelectionData selection, ItemInstanceIdentityRuntime itemRuntime, ItemDurabilityRuntime durabilityRuntime)
        {
            if (!string.IsNullOrWhiteSpace(selection.selectedToolItemInstanceId))
            {
                if (itemRuntime != null && itemRuntime.TryGetSnapshot(selection.selectedToolItemInstanceId, out ItemInstanceSnapshot item))
                {
                    plan.dependencies.Add(new ProductionPlanDependencyData { dependencyType = "item", dependencyId = selection.selectedToolItemInstanceId, revision = item.Revision });
                }

                if (durabilityRuntime != null && durabilityRuntime.TryGetDurabilityForItem(selection.selectedToolItemInstanceId, out ItemDurabilitySnapshot durability))
                {
                    plan.dependencies.Add(new ProductionPlanDependencyData { dependencyType = "durability", dependencyId = selection.selectedToolItemInstanceId, revision = durability.Revision });
                }
            }

            if (!string.IsNullOrWhiteSpace(selection.selectedStationInstanceId))
            {
                long stationRevision = stationsById.TryGetValue(selection.selectedStationInstanceId, out ProductionStationInstanceData station) ? station.revision : -1L;
                plan.dependencies.Add(new ProductionPlanDependencyData { dependencyType = "station", dependencyId = selection.selectedStationInstanceId, revision = stationRevision });
            }

            foreach (ProductionInputAllocationData allocation in selection.allocations ?? new List<ProductionInputAllocationData>())
            {
                string sourceKey = AllocationSourceKey(allocation);
                if (!string.IsNullOrWhiteSpace(sourceKey))
                {
                    plan.dependencies.Add(new ProductionPlanDependencyData { dependencyType = $"allocation.{allocation.requirementType}", dependencyId = sourceKey, revision = Math.Max(allocation.expectedRuntimeRevision, allocation.expectedStackRevision) });
                }

                if (!string.IsNullOrWhiteSpace(allocation.itemInstanceId) && itemRuntime != null && itemRuntime.TryGetSnapshot(allocation.itemInstanceId, out ItemInstanceSnapshot item))
                {
                    plan.dependencies.Add(new ProductionPlanDependencyData { dependencyType = "item", dependencyId = allocation.itemInstanceId, revision = item.Revision });
                }
            }
        }

        private long CurrentDependencyRevision(ProductionPlanDependencyData dependency, ItemInstanceIdentityRuntime itemRuntime, ItemDurabilityRuntime durabilityRuntime)
        {
            if (dependency == null)
            {
                return 0L;
            }

            if (string.Equals(dependency.dependencyType, "item", StringComparison.Ordinal))
            {
                return itemRuntime != null && itemRuntime.TryGetSnapshot(dependency.dependencyId, out ItemInstanceSnapshot item) ? item.Revision : -1L;
            }

            if (string.Equals(dependency.dependencyType, "durability", StringComparison.Ordinal))
            {
                return durabilityRuntime != null && durabilityRuntime.TryGetDurabilityForItem(dependency.dependencyId, out ItemDurabilitySnapshot durability) ? durability.Revision : -1L;
            }

            if (string.Equals(dependency.dependencyType, "station", StringComparison.Ordinal))
            {
                return stationsById.TryGetValue(dependency.dependencyId, out ProductionStationInstanceData station) ? station.revision : -1L;
            }

            return dependency.revision;
        }

        private bool HasActiveReservationForTool(string itemInstanceId, string samePlanId)
        {
            return reservationsById.Values.Any(reservation => reservation.status == ProductionReservationStatus.Active
                && !string.IsNullOrWhiteSpace(itemInstanceId)
                && string.Equals(reservation.reservedToolItemInstanceId, itemInstanceId, StringComparison.Ordinal)
                && !string.Equals(reservation.planId, samePlanId, StringComparison.Ordinal));
        }

        private float ReservedQuantityFor(ProductionQuantityData source, ProductionRequirementType type, string samePlanId = "")
        {
            string sourceKey = QuantitySourceKey(source);
            return reservationsById.Values
                .Where(reservation => reservation.status == ProductionReservationStatus.Active
                    && reservation.reservedRequirementType == type
                    && !string.Equals(reservation.planId, samePlanId, StringComparison.Ordinal)
                    && string.Equals(ReservationSourceKey(reservation), sourceKey, StringComparison.Ordinal))
                .Sum(reservation => reservation.reservedQuantity);
        }

        private bool AllocationHasReservationCapacity(ProductionInputAllocationData allocation, string samePlanId)
        {
            if (allocation == null || allocation.reusable)
            {
                return true;
            }

            float active = reservationsById.Values
                .Where(reservation => reservation.status == ProductionReservationStatus.Active
                    && reservation.reservedRequirementType == allocation.requirementType
                    && !string.Equals(reservation.planId, samePlanId, StringComparison.Ordinal)
                    && string.Equals(ReservationSourceKey(reservation), AllocationSourceKey(allocation), StringComparison.Ordinal))
                .Sum(reservation => reservation.reservedQuantity);
            return active + allocation.quantity <= allocation.sourceTotalQuantity + 0.0001f;
        }

        private static string QuantitySourceKey(ProductionQuantityData source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            return FirstNonEmpty(source.itemInstanceId, source.sourceContainerId, $"{source.definitionId}@{source.locationId}");
        }

        private static string AllocationSourceKey(ProductionInputAllocationData allocation)
        {
            if (allocation == null)
            {
                return string.Empty;
            }

            return FirstNonEmpty(allocation.itemInstanceId, allocation.sourceContainerId, $"{allocation.definitionId}@{allocation.locationId}");
        }

        private static string ReservationSourceKey(ProductionReservationData reservation)
        {
            if (reservation == null)
            {
                return string.Empty;
            }

            return FirstNonEmpty(reservation.reservedItemInstanceId, reservation.sourceContainerId, $"{reservation.reservedDefinitionId}@{reservation.locationId}");
        }

        private bool StationHasCapacity(string stationInstanceId, string samePlanId)
        {
            if (string.IsNullOrWhiteSpace(stationInstanceId) || !stationsById.TryGetValue(stationInstanceId, out ProductionStationInstanceData station))
            {
                return false;
            }

            int active = reservationsById.Values.Count(reservation => reservation.status == ProductionReservationStatus.Active
                && string.Equals(reservation.reservedStationInstanceId, stationInstanceId, StringComparison.Ordinal)
                && !string.Equals(reservation.planId, samePlanId, StringComparison.Ordinal));
            return active < Math.Max(1, station.reservationLimit);
        }

        private void ExpireReservations(string currentWorldTime)
        {
            if (!TryParseTime(currentWorldTime, out double now))
            {
                return;
            }

            foreach (ProductionReservationData reservation in reservationsById.Values.Where(reservation => reservation.status == ProductionReservationStatus.Active).ToArray())
            {
                if (TryParseTime(reservation.expiresWorldTime, out double expiry) && expiry <= now)
                {
                    reservation.status = ProductionReservationStatus.Expired;
                    reservation.revision++;
                }
            }
        }

        private static bool TryParseTime(string value, out double parsed)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed);
        }

        private static string ComputeSignature(ProductionRequirementPlanData plan)
        {
            string selectionText = string.Join("|", (plan.selections ?? new List<ProductionRequirementSelectionData>())
                .OrderBy(selection => selection.requirementId, StringComparer.Ordinal)
                .Select(selection =>
                {
                    string allocationText = string.Join(",", (selection.allocations ?? new List<ProductionInputAllocationData>())
                        .OrderBy(allocation => allocation.allocationId, StringComparer.Ordinal)
                        .Select(allocation => $"{allocation.allocationId}:{allocation.definitionId}:{allocation.itemInstanceId}:{allocation.sourceContainerId}:{allocation.locationId}:{allocation.quantity:0.######}:{allocation.unit}:{allocation.expectedRuntimeRevision}:{allocation.expectedStackRevision}:{allocation.substitutionUsed}:{allocation.accessDecisionId}"));
                    return $"{selection.requirementId}:{selection.requirementType}:{selection.selectedToolItemInstanceId}:{selection.selectedToolDefinitionId}:{selection.selectedStationInstanceId}:{selection.selectedStationDefinitionId}:{selection.selectedDefinitionId}:{selection.quantity:0.######}:{selection.alternativeUsed}:{selection.expectedToolWear:0.######}:{allocationText}";
                }));
            string dependencyText = string.Join("|", (plan.dependencies ?? new List<ProductionPlanDependencyData>())
                .OrderBy(dependency => dependency.dependencyType, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.dependencyId, StringComparer.Ordinal)
                .Select(dependency => $"{dependency.dependencyType}:{dependency.dependencyId}:{dependency.revision}"));
            return StableId("production-plan-signature", plan.productionJobId, selectionText, dependencyText);
        }

        private static string StableId(string prefix, params string[] parts)
        {
            using MD5 md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", parts ?? Array.Empty<string>())));
            return $"{prefix}.{new Guid(bytes):N}";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private readonly struct RequirementSpec
        {
            private RequirementSpec(
                ProductionRequirementType type,
                ProductionToolDefinition toolDefinition,
                ProductionToolRole toolRole,
                ProductionToolCategory toolCategory,
                string toolCapabilityId,
                ProductionStationDefinition stationDefinition,
                ProductionStationCategory stationCategory,
                string stationCapabilityId,
                string capabilityId,
                string knowledgeFactDefinitionId,
                string resourceId,
                string itemDefinitionId,
                string materialDefinitionId,
                float quantity,
                ProductionQuantityUnit quantityUnit,
                string environmentKey,
                string accessKey,
                string bodyCapabilityId,
                bool allowSubstitution)
            {
                Type = type;
                ToolDefinition = toolDefinition;
                ToolRole = toolRole;
                ToolCategory = toolCategory;
                ToolCapabilityId = toolCapabilityId ?? string.Empty;
                StationDefinition = stationDefinition;
                StationCategory = stationCategory;
                StationCapabilityId = stationCapabilityId ?? string.Empty;
                CapabilityId = capabilityId ?? string.Empty;
                KnowledgeFactDefinitionId = knowledgeFactDefinitionId ?? string.Empty;
                ResourceId = resourceId ?? string.Empty;
                ItemDefinitionId = itemDefinitionId ?? string.Empty;
                MaterialDefinitionId = materialDefinitionId ?? string.Empty;
                Quantity = quantity <= 0f ? 1f : quantity;
                QuantityUnit = quantityUnit;
                EnvironmentKey = environmentKey ?? string.Empty;
                AccessKey = accessKey ?? string.Empty;
                BodyCapabilityId = bodyCapabilityId ?? string.Empty;
                AllowSubstitution = allowSubstitution;
            }

            public ProductionRequirementType Type { get; }
            public ProductionToolDefinition ToolDefinition { get; }
            public string ToolDefinitionId => ToolDefinition == null ? string.Empty : ToolDefinition.Id;
            public ProductionToolRole ToolRole { get; }
            public ProductionToolCategory ToolCategory { get; }
            public string ToolCapabilityId { get; }
            public ProductionStationDefinition StationDefinition { get; }
            public string StationDefinitionId => StationDefinition == null ? string.Empty : StationDefinition.Id;
            public ProductionStationCategory StationCategory { get; }
            public string StationCapabilityId { get; }
            public string CapabilityId { get; }
            public string KnowledgeFactDefinitionId { get; }
            public string ResourceId { get; }
            public string ItemDefinitionId { get; }
            public string MaterialDefinitionId { get; }
            public float Quantity { get; }
            public ProductionQuantityUnit QuantityUnit { get; }
            public string EnvironmentKey { get; }
            public string AccessKey { get; }
            public string BodyCapabilityId { get; }
            public bool AllowSubstitution { get; }

            public static RequirementSpec From(ProductionRequirementDefinition requirement)
            {
                return new RequirementSpec(
                    requirement.RequirementType,
                    requirement.ToolDefinition,
                    requirement.ToolRole,
                    requirement.ToolCategory,
                    requirement.ToolCapabilityId,
                    requirement.StationDefinition,
                    requirement.StationCategory,
                    requirement.StationCapabilityId,
                    requirement.CapabilityId,
                    requirement.KnowledgeFactDefinitionId,
                    requirement.ResourceId,
                    requirement.ItemDefinitionId,
                    requirement.MaterialDefinitionId,
                    requirement.Quantity,
                    requirement.QuantityUnit,
                    requirement.EnvironmentKey,
                    requirement.AccessKey,
                    requirement.BodyCapabilityId,
                    requirement.AllowSubstitution);
            }

            public static RequirementSpec From(ProductionRequirementDefinition owner, ProductionRequirementAlternativeDefinition alternative)
            {
                return new RequirementSpec(
                    alternative.RequirementType == ProductionRequirementType.Unknown ? owner.RequirementType : alternative.RequirementType,
                    alternative.ToolDefinition,
                    alternative.ToolRole,
                    alternative.ToolCategory,
                    alternative.ToolCapabilityId,
                    alternative.StationDefinition,
                    alternative.StationCategory,
                    alternative.StationCapabilityId,
                    alternative.CapabilityId,
                    alternative.KnowledgeFactDefinitionId,
                    owner.ResourceId,
                    alternative.ItemDefinitionId,
                    alternative.MaterialDefinitionId,
                    alternative.Quantity <= 0f ? owner.Quantity : alternative.Quantity,
                    alternative.QuantityUnit,
                    owner.EnvironmentKey,
                    owner.AccessKey,
                    owner.BodyCapabilityId,
                    owner.AllowSubstitution);
            }
        }
    }
}
