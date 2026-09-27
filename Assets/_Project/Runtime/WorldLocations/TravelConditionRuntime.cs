using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class TravelConditionRuntime : IDisposable
    {
        private readonly Dictionary<string, TravelConditionRecordData> conditionsById = new Dictionary<string, TravelConditionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelHazardExposureRecordData> hazardsById = new Dictionary<string, TravelHazardExposureRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelEncounterRecordData> encountersById = new Dictionary<string, TravelEncounterRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TravelConditionTransactionRecordData> transactionsById = new Dictionary<string, TravelConditionTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> conditionIdsByTargetKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private LocationRouteRuntime routes;
        private TravelJourneyRuntime journeys;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public event Action<TravelHazardExposureSnapshot> HazardTriggered;
        public event Action<TravelEncounterSnapshot> EncounterTriggered;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public string WorldId => worldId;
        public int ConditionCount => conditionsById.Count;
        public int HazardExposureCount => hazardsById.Count;
        public int EncounterCount => encountersById.Count;
        public IReadOnlyList<TravelConditionSnapshot> Conditions => conditionsById.Values.OrderBy(item => item.conditionId, StringComparer.Ordinal).Select(item => new TravelConditionSnapshot(item)).ToArray();
        public IReadOnlyList<TravelHazardExposureSnapshot> HazardExposures => hazardsById.Values.OrderBy(item => item.hazardExposureId, StringComparer.Ordinal).Select(item => new TravelHazardExposureSnapshot(item)).ToArray();
        public IReadOnlyList<TravelEncounterSnapshot> Encounters => encountersById.Values.OrderBy(item => item.encounterId, StringComparer.Ordinal).Select(item => new TravelEncounterSnapshot(item)).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, LocationRouteRuntime routeRuntime = null, TravelJourneyRuntime journeyRuntime = null, string runtimeWorldId = PersistenceService.LocalWorldId)
        {
            registry = definitionRegistry ?? registry;
            routes = routeRuntime ?? routes;
            journeys = journeyRuntime ?? journeys;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId.Trim();
            disposed = false;
            RebuildIndexes();
        }

        public TravelConditionOperationResult CreateCondition(TravelConditionCreateRequest request)
        {
            request ??= new TravelConditionCreateRequest();
            long before = Revision;
            if (!Ready(before, out TravelConditionOperationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out TravelConditionOperationResult revisionFailure)) return revisionFailure;
            string id = N(request.conditionId);
            if (TryDuplicate(N(request.transactionId), id, "travel-condition.create", before, out TravelConditionOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id)) return Fail(TravelConditionMutationStatus.InvalidRequest, "Travel condition ID is required.", before);
            if (conditionsById.ContainsKey(id)) return Fail(TravelConditionMutationStatus.Duplicate, $"Travel condition '{id}' already exists.", before);
            if (!TryGetConditionDefinition(request.conditionDefinitionId, before, out TravelConditionDefinition definition, out TravelConditionOperationResult failure)) return failure;
            TravelConditionTargetReferenceData target = request.target?.Clone();
            if (!ValidateTarget(target, definition, before, out failure)) return failure;
            TravelConditionLifecycleState state = request.lifecycleState == TravelConditionLifecycleState.Unknown ? TravelConditionLifecycleState.Active : request.lifecycleState;
            if (!ValidLifecycle(state)) return Fail(TravelConditionMutationStatus.InvalidRequest, $"Travel condition lifecycle '{state}' is invalid.", before);
            TravelConditionSeverity severity = request.severity == TravelConditionSeverity.Unknown ? definition.DefaultSeverity : request.severity;
            if (!Enum.IsDefined(typeof(TravelConditionSeverity), severity) || severity == TravelConditionSeverity.Unknown) return Fail(TravelConditionMutationStatus.InvalidRequest, "Travel condition severity is invalid.", before);
            double movement = request.movementRateMultiplier > 0d ? request.movementRateMultiplier : definition.MovementRateMultiplier;
            double cost = request.routeCostMultiplier > 0d ? request.routeCostMultiplier : definition.RouteCostMultiplier;
            if (!ValidPositive(movement) || !ValidPositive(cost)) return Fail(TravelConditionMutationStatus.InvalidRequest, "Travel condition multipliers must be positive.", before);
            TravelConditionVisibility visibility = request.visibility == default ? definition.DefaultVisibility : request.visibility;
            double end = request.endsWorldTime >= 0d ? request.endsWorldTime : definition.DefaultDurationSeconds >= 0d ? request.startsWorldTime + definition.DefaultDurationSeconds : -1d;
            if (end >= 0d && end < request.startsWorldTime) return Fail(TravelConditionMutationStatus.InvalidRequest, "Travel condition end time cannot be before start time.", before);
            TravelConditionRecordData record = new TravelConditionRecordData
            {
                conditionId = id,
                conditionDefinitionId = definition.Id,
                worldId = worldId,
                target = target,
                lifecycleState = state,
                severity = severity,
                visibility = visibility,
                movementRateMultiplier = movement,
                routeCostMultiplier = cost,
                hardBlocksTravel = request.hardBlocksTravel ?? definition.HardBlocksTravel,
                additionalRequiredCapabilityIds = Clean(request.additionalRequiredCapabilityIds),
                additionalRequiredEquipmentDefinitionIds = Clean(request.additionalRequiredEquipmentDefinitionIds),
                startsWorldTime = request.startsWorldTime,
                endsWorldTime = end,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return TravelConditionOperationResult.Success(new TravelConditionSnapshot(record), "Travel condition create preview.", before, before, preview: true);

            conditionsById[id] = record;
            Complete(N(request.transactionId), "travel-condition.create", id, id);
            Touch();
            RebuildIndexes();
            return TravelConditionOperationResult.Success(new TravelConditionSnapshot(record), "Travel condition created.", before, Revision);
        }

        public TravelConditionOperationResult MutateCondition(TravelConditionMutationRequest request)
        {
            request ??= new TravelConditionMutationRequest();
            long before = Revision;
            if (!Ready(before, out TravelConditionOperationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out TravelConditionOperationResult revisionFailure)) return revisionFailure;
            string id = N(request.conditionId);
            if (TryDuplicate(N(request.transactionId), id, "travel-condition.mutate", before, out TravelConditionOperationResult duplicate)) return duplicate;
            if (!conditionsById.TryGetValue(id, out TravelConditionRecordData existing)) return Fail(TravelConditionMutationStatus.MissingCondition, $"Travel condition '{id}' is missing.", before);
            TravelConditionRecordData changed = existing.Clone();
            if (request.lifecycleState != TravelConditionLifecycleState.Unknown)
            {
                if (!ValidLifecycle(request.lifecycleState)) return Fail(TravelConditionMutationStatus.InvalidRequest, $"Travel condition lifecycle '{request.lifecycleState}' is invalid.", before);
                changed.lifecycleState = request.lifecycleState;
                if (request.lifecycleState is TravelConditionLifecycleState.Expired or TravelConditionLifecycleState.Resolved or TravelConditionLifecycleState.Historical)
                {
                    changed.endsWorldTime = request.worldTime;
                }
            }
            if (request.endsWorldTime > -2d)
            {
                if (request.endsWorldTime >= 0d && request.endsWorldTime < changed.startsWorldTime) return Fail(TravelConditionMutationStatus.InvalidRequest, "Travel condition end time cannot be before start time.", before);
                changed.endsWorldTime = request.endsWorldTime;
            }
            if (request.preview) return TravelConditionOperationResult.Success(new TravelConditionSnapshot(changed), "Travel condition mutation preview.", before, before, preview: true);

            changed.revision++;
            conditionsById[id] = changed;
            Complete(N(request.transactionId), "travel-condition.mutate", id, id);
            Touch();
            RebuildIndexes();
            return TravelConditionOperationResult.Success(new TravelConditionSnapshot(changed), "Travel condition updated.", before, Revision);
        }

        public TravelConditionEvaluationResult Evaluate(TravelConditionEvaluationRequest request)
        {
            request ??= new TravelConditionEvaluationRequest();
            if (request.evaluationMode == TravelConditionEvaluationMode.IgnoreDynamicConditions) return TravelConditionEvaluationResult.Empty(Revision);
            if (disposed || registry == null) return TravelConditionEvaluationResult.Failure("Travel condition runtime is not ready.", Revision);
            TravelConditionTargetReferenceData target = request.target?.Clone();
            if (target == null || target.scope == TravelConditionTargetScope.Unknown) return TravelConditionEvaluationResult.Empty(Revision);

            HashSet<string> capabilities = new HashSet<string>(Clean(request.travelerCapabilityIds), StringComparer.Ordinal);
            HashSet<string> equipment = new HashSet<string>(Clean(request.travelerEquipmentDefinitionIds), StringComparer.Ordinal);
            HashSet<string> knownConditions = new HashSet<string>(Clean(request.knownConditionIds), StringComparer.Ordinal);
            HashSet<string> knownEncounters = new HashSet<string>(Clean(request.knownEncounterIds), StringComparer.Ordinal);
            List<TravelConditionApplicableSnapshot> visible = new List<TravelConditionApplicableSnapshot>();
            List<TravelConditionRecordData> effective = new List<TravelConditionRecordData>();
            List<string> requiredCapabilities = new List<string>();
            List<string> requiredEquipment = new List<string>();
            List<string> missingCapabilities = new List<string>();
            List<string> missingEquipment = new List<string>();
            List<string> visibleEncounterIds = new List<string>();
            List<string> hiddenKnownEncounterIds = new List<string>();
            bool hardBlocked = false;
            string blockReason = string.Empty;

            foreach (TravelConditionRecordData condition in ApplicableConditions(target, request.worldTime))
            {
                if (!registry.TryGet(condition.conditionDefinitionId, out TravelConditionDefinition definition)) continue;
                bool hidden = IsHidden(condition.visibility) || IsHidden(definition.DefaultVisibility);
                bool knowledgeSafe = request.evaluationMode == TravelConditionEvaluationMode.KnowledgeSafeCurrentConditions && !request.includeHiddenDevelopmentConditions;
                bool known = knownConditions.Contains(condition.conditionId);
                if (knowledgeSafe && hidden && !known) continue;

                effective.Add(condition);
                visible.Add(new TravelConditionApplicableSnapshot(new TravelConditionSnapshot(condition), hidden && knowledgeSafe ? "Restricted travel condition" : definition.DisplayName, definition.Category, condition.severity, hidden && knowledgeSafe));
                if (condition.hardBlocksTravel || definition.HardBlocksTravel)
                {
                    hardBlocked = true;
                    blockReason = First(blockReason, $"condition:{condition.conditionId}");
                }
                if (!string.IsNullOrWhiteSpace(request.travelModeDefinitionId) && definition.RestrictedTravelModeDefinitionIds.Contains(N(request.travelModeDefinitionId), StringComparer.Ordinal))
                {
                    hardBlocked = true;
                    blockReason = First(blockReason, $"mode-restricted:{request.travelModeDefinitionId}");
                }

                foreach (string capabilityId in definition.RequiredCapabilityIds.Concat(condition.additionalRequiredCapabilityIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal))
                {
                    requiredCapabilities.Add(capabilityId);
                    if (!capabilities.Contains(capabilityId))
                    {
                        missingCapabilities.Add(capabilityId);
                        hardBlocked = true;
                    }
                }

                foreach (string equipmentId in definition.RequiredEquipmentDefinitionIds.Concat(condition.additionalRequiredEquipmentDefinitionIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal))
                {
                    requiredEquipment.Add(equipmentId);
                    if (!equipment.Contains(equipmentId))
                    {
                        missingEquipment.Add(equipmentId);
                        hardBlocked = true;
                    }
                }

                foreach (string encounterId in definition.EncounterDefinitionIds)
                {
                    if (!registry.TryGet(encounterId, out TravelEncounterDefinition encounterDefinition)) continue;
                    bool encounterHidden = IsHidden(encounterDefinition.Visibility);
                    if (!knowledgeSafe || !encounterHidden) visibleEncounterIds.Add(encounterId);
                    else if (knownEncounters.Contains(encounterId)) hiddenKnownEncounterIds.Add(encounterId);
                }
            }

            double movement = AggregateMultiplier(effective, definition => definition.MovementRateMultiplier, condition => condition.movementRateMultiplier);
            double cost = AggregateMultiplier(effective, definition => definition.RouteCostMultiplier, condition => condition.routeCostMultiplier);
            string diagnostics = hardBlocked
                ? $"Travel blocked by {First(blockReason, "requirements")}."
                : effective.Count == 0 ? "No applicable travel conditions." : $"Applied {effective.Count} travel condition(s).";
            return new TravelConditionEvaluationResult(true, hardBlocked, movement, cost, visible, requiredCapabilities, requiredEquipment, missingCapabilities, missingEquipment, new TravelConditionEncounterRiskSummary(visibleEncounterIds, hiddenKnownEncounterIds), Revision, diagnostics);
        }

        public TravelConditionOperationResult TriggerHazard(TravelHazardTriggerRequest request)
        {
            request ??= new TravelHazardTriggerRequest();
            long before = Revision;
            if (!Ready(before, out TravelConditionOperationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out TravelConditionOperationResult revisionFailure)) return revisionFailure;
            string id = First(request.hazardExposureId, $"travel-hazard-exposure.{StableSuffix($"{request.hazardDefinitionId}:{request.sourceConditionId}:{request.target?.StableKey}:{request.traveler?.StableKey}")}");
            if (TryDuplicate(N(request.transactionId), id, "travel-hazard.trigger", before, out TravelConditionOperationResult duplicate)) return duplicate;
            if (hazardsById.TryGetValue(id, out TravelHazardExposureRecordData existing)) return TravelConditionOperationResult.HazardSuccess(new TravelHazardExposureSnapshot(existing), "Travel hazard trigger already recorded.", before, before, duplicate: true);
            if (!TryGetHazardDefinition(request.hazardDefinitionId, before, out TravelHazardDefinition definition, out TravelConditionOperationResult failure)) return failure;
            TravelHazardExposureRecordData record = new TravelHazardExposureRecordData
            {
                hazardExposureId = id,
                hazardDefinitionId = definition.Id,
                sourceConditionId = N(request.sourceConditionId),
                worldId = worldId,
                target = request.target?.Clone(),
                traveler = request.traveler?.Clone(),
                lifecycleState = TravelHazardExposureLifecycleState.Triggered,
                outcome = request.outcome == TravelHazardOutcome.None ? TravelHazardOutcome.Exposed : request.outcome,
                createdWorldTime = request.worldTime,
                triggeredWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return TravelConditionOperationResult.HazardSuccess(new TravelHazardExposureSnapshot(record), "Travel hazard trigger preview.", before, before, preview: true);
            hazardsById[id] = record;
            Complete(N(request.transactionId), "travel-hazard.trigger", id, id);
            Touch();
            HazardTriggered?.Invoke(new TravelHazardExposureSnapshot(record));
            return TravelConditionOperationResult.HazardSuccess(new TravelHazardExposureSnapshot(record), "Travel hazard triggered.", before, Revision);
        }

        public TravelConditionOperationResult TriggerEncounter(TravelEncounterTriggerRequest request)
        {
            request ??= new TravelEncounterTriggerRequest();
            long before = Revision;
            if (!Ready(before, out TravelConditionOperationResult ready)) return ready;
            if (!ValidateRevision(request.expectedRevision, before, out TravelConditionOperationResult revisionFailure)) return revisionFailure;
            string id = First(request.encounterId, $"travel-encounter.{StableSuffix($"{request.encounterDefinitionId}:{request.sourceConditionId}:{request.journeyId}:{request.target?.StableKey}:{request.traveler?.StableKey}")}");
            if (TryDuplicate(N(request.transactionId), id, "travel-encounter.trigger", before, out TravelConditionOperationResult duplicate)) return duplicate;
            if (encountersById.TryGetValue(id, out TravelEncounterRecordData existing)) return TravelConditionOperationResult.EncounterSuccess(new TravelEncounterSnapshot(existing), "Travel encounter already recorded.", before, before, duplicate: true);
            if (!TryGetEncounterDefinition(request.encounterDefinitionId, before, out TravelEncounterDefinition definition, out TravelConditionOperationResult failure)) return failure;
            TravelEncounterRecordData record = new TravelEncounterRecordData
            {
                encounterId = id,
                encounterDefinitionId = definition.Id,
                sourceConditionId = N(request.sourceConditionId),
                worldId = worldId,
                target = request.target?.Clone(),
                journeyId = N(request.journeyId),
                traveler = request.traveler?.Clone(),
                participantReferenceKeys = Clean(request.participantReferenceKeys),
                lifecycleState = TravelEncounterLifecycleState.Triggered,
                createdWorldTime = request.worldTime,
                triggeredWorldTime = request.worldTime,
                sourceEventId = N(request.sourceEventId),
                sourceRecordId = N(request.sourceRecordId),
                provenanceId = N(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return TravelConditionOperationResult.EncounterSuccess(new TravelEncounterSnapshot(record), "Travel encounter trigger preview.", before, before, preview: true);
            encountersById[id] = record;
            Complete(N(request.transactionId), "travel-encounter.trigger", id, id);
            Touch();
            EncounterTriggered?.Invoke(new TravelEncounterSnapshot(record));
            return TravelConditionOperationResult.EncounterSuccess(new TravelEncounterSnapshot(record), "Travel encounter triggered.", before, Revision);
        }

        public TravelConditionOperationResult EvaluateJourneyCheckpoint(string journeyId, TravelConditionTargetReferenceData target, EntityLocationReferenceData traveler, string travelModeDefinitionId, IEnumerable<string> capabilities, IEnumerable<string> equipment, double worldTime)
        {
            TravelConditionEvaluationResult evaluation = Evaluate(new TravelConditionEvaluationRequest
            {
                evaluationMode = TravelConditionEvaluationMode.CurrentConditions,
                target = target,
                traveler = traveler,
                travelModeDefinitionId = travelModeDefinitionId,
                travelerCapabilityIds = Clean(capabilities),
                travelerEquipmentDefinitionIds = Clean(equipment),
                worldTime = worldTime
            });
            if (!evaluation.Succeeded || evaluation.HardBlocked) return TravelConditionOperationResult.EvaluationSuccess(evaluation, evaluation.Diagnostics, Revision);
            foreach (TravelConditionApplicableSnapshot condition in evaluation.ApplicableConditions.OrderBy(item => item.Condition.ConditionId, StringComparer.Ordinal))
            {
                if (!registry.TryGet(condition.Condition.ConditionDefinitionId, out TravelConditionDefinition conditionDefinition)) continue;
                foreach (string encounterDefinitionId in conditionDefinition.EncounterDefinitionIds.OrderBy(id => id, StringComparer.Ordinal))
                {
                    if (!registry.TryGet(encounterDefinitionId, out TravelEncounterDefinition encounterDefinition)) continue;
                    if (encounterDefinition.TriggerPolicy != TravelEncounterTriggerPolicy.JourneyCheckpoint) continue;
                    if (EncounterAlreadyTriggered(encounterDefinition, journeyId, condition.Condition.ConditionId, target)) continue;
                    return TriggerEncounter(new TravelEncounterTriggerRequest
                    {
                        transactionId = $"travel-encounter.checkpoint.{journeyId}.{condition.Condition.ConditionId}.{encounterDefinitionId}",
                        encounterDefinitionId = encounterDefinitionId,
                        sourceConditionId = condition.Condition.ConditionId,
                        target = target,
                        journeyId = journeyId,
                        traveler = traveler,
                        worldTime = worldTime,
                        provenanceId = "travel-condition.checkpoint"
                    });
                }
            }

            return TravelConditionOperationResult.EvaluationSuccess(evaluation, "No checkpoint encounter triggered.", Revision);
        }

        public bool TryGetCondition(string conditionId, out TravelConditionSnapshot snapshot)
        {
            if (conditionsById.TryGetValue(N(conditionId), out TravelConditionRecordData record))
            {
                snapshot = new TravelConditionSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public TravelConditionRuntimeSaveData CreateSaveData()
        {
            return new TravelConditionRuntimeSaveData
            {
                schemaVersion = TravelConditionRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                conditions = conditionsById.Values.OrderBy(item => item.conditionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                hazardExposures = hazardsById.Values.OrderBy(item => item.hazardExposureId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                encounters = encountersById.Values.OrderBy(item => item.encounterId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactionsById.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray()
            };
        }

        public TravelConditionOperationResult RestoreFromSaveData(TravelConditionRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null, LocationRouteRuntime routeRuntime = null, TravelJourneyRuntime journeyRuntime = null, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, routeRuntime ?? routes, expectedWorldId, out string failure)) return Fail(TravelConditionMutationStatus.PersistenceInvalid, failure, before);
            TravelConditionRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData ?? new TravelConditionRuntimeSaveData());
                registry = definitionRegistry ?? registry;
                routes = routeRuntime ?? routes;
                journeys = journeyRuntime ?? journeys;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                IsDirty = !restoring;
                RebuildIndexes();
                return TravelConditionOperationResult.Success(null, "Travel conditions restored.", before, Revision);
            }
            catch (Exception exception)
            {
                RestoreInternal(rollback);
                RebuildIndexes();
                return Fail(TravelConditionMutationStatus.RestoreFailed, $"Travel condition restore failed: {exception.Message}", before);
            }
        }

        public bool ValidateCurrent(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), registry, routes, worldId, out failure);
        }

        public static bool ValidateSaveData(TravelConditionRuntimeSaveData saveData, DefinitionRegistry registry, LocationRouteRuntime routes, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            if (saveData == null) errors.Add("Travel condition save data is missing.");
            else
            {
                if (saveData.schemaVersion != TravelConditionRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported travel condition schema version {saveData.schemaVersion}.");
                string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId, world, StringComparison.Ordinal)) errors.Add($"Travel condition world '{saveData.worldId}' does not match expected world '{world}'.");
                HashSet<string> conditionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (TravelConditionRecordData condition in saveData.conditions ?? Array.Empty<TravelConditionRecordData>())
                {
                    if (condition == null) { errors.Add("Travel condition record is null."); continue; }
                    if (string.IsNullOrWhiteSpace(condition.conditionId)) errors.Add("Travel condition record is missing condition ID.");
                    else if (!conditionIds.Add(condition.conditionId)) errors.Add($"Duplicate travel condition '{condition.conditionId}'.");
                    if (registry == null || !registry.TryGet(condition.conditionDefinitionId, out TravelConditionDefinition _)) errors.Add($"Travel condition '{condition.conditionId}' references missing definition '{condition.conditionDefinitionId}'.");
                    if (condition.target == null || condition.target.scope == TravelConditionTargetScope.Unknown) errors.Add($"Travel condition '{condition.conditionId}' has invalid target.");
                    if (!ValidPositive(condition.movementRateMultiplier) || !ValidPositive(condition.routeCostMultiplier)) errors.Add($"Travel condition '{condition.conditionId}' has invalid multiplier.");
                    if (condition.endsWorldTime >= 0d && condition.endsWorldTime < condition.startsWorldTime) errors.Add($"Travel condition '{condition.conditionId}' has end time before start time.");
                }
                foreach (TravelHazardExposureRecordData hazard in saveData.hazardExposures ?? Array.Empty<TravelHazardExposureRecordData>())
                {
                    if (hazard == null) { errors.Add("Travel hazard exposure record is null."); continue; }
                    if (string.IsNullOrWhiteSpace(hazard.hazardExposureId)) errors.Add("Travel hazard exposure is missing ID.");
                    if (registry == null || !registry.TryGet(hazard.hazardDefinitionId, out TravelHazardDefinition _)) errors.Add($"Travel hazard exposure '{hazard.hazardExposureId}' references missing definition '{hazard.hazardDefinitionId}'.");
                    if (!string.IsNullOrWhiteSpace(hazard.sourceConditionId) && !conditionIds.Contains(hazard.sourceConditionId)) errors.Add($"Travel hazard exposure '{hazard.hazardExposureId}' references missing condition '{hazard.sourceConditionId}'.");
                }
                foreach (TravelEncounterRecordData encounter in saveData.encounters ?? Array.Empty<TravelEncounterRecordData>())
                {
                    if (encounter == null) { errors.Add("Travel encounter record is null."); continue; }
                    if (string.IsNullOrWhiteSpace(encounter.encounterId)) errors.Add("Travel encounter is missing ID.");
                    if (registry == null || !registry.TryGet(encounter.encounterDefinitionId, out TravelEncounterDefinition _)) errors.Add($"Travel encounter '{encounter.encounterId}' references missing definition '{encounter.encounterDefinitionId}'.");
                    if (!string.IsNullOrWhiteSpace(encounter.sourceConditionId) && !conditionIds.Contains(encounter.sourceConditionId)) errors.Add($"Travel encounter '{encounter.encounterId}' references missing condition '{encounter.sourceConditionId}'.");
                }
            }

            failure = string.Join(" | ", errors);
            return errors.Count == 0;
        }

        public void Dispose()
        {
            disposed = true;
        }

        private IEnumerable<TravelConditionRecordData> ApplicableConditions(TravelConditionTargetReferenceData target, double worldTime)
        {
            HashSet<string> candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in CandidateKeys(target))
            {
                foreach (string id in GetIds(conditionIdsByTargetKey, key)) candidates.Add(id);
            }
            foreach (TravelConditionRecordData condition in candidates.Select(id => conditionsById.TryGetValue(id, out TravelConditionRecordData record) ? record : null).Where(record => record != null))
            {
                if (!IsActiveAt(condition, worldTime)) continue;
                if (!registry.TryGet(condition.conditionDefinitionId, out TravelConditionDefinition definition)) continue;
                if (!definition.SupportsScope(condition.target?.scope ?? TravelConditionTargetScope.Unknown)) continue;
                yield return condition;
            }
        }

        private double AggregateMultiplier(IEnumerable<TravelConditionRecordData> records, Func<TravelConditionDefinition, double> definitionValue, Func<TravelConditionRecordData, double> recordValue)
        {
            double result = 1d;
            foreach (TravelConditionRecordData record in records.OrderByDescending(Priority).ThenBy(record => record.conditionId, StringComparer.Ordinal))
            {
                double value = recordValue(record);
                if (!ValidPositive(value) && registry.TryGet(record.conditionDefinitionId, out TravelConditionDefinition definition)) value = definitionValue(definition);
                result *= ValidPositive(value) ? value : 1d;
            }
            return Math.Max(0.0001d, result);
        }

        private bool EncounterAlreadyTriggered(TravelEncounterDefinition definition, string journeyId, string conditionId, TravelConditionTargetReferenceData target)
        {
            foreach (TravelEncounterRecordData encounter in encountersById.Values)
            {
                if (!string.Equals(encounter.encounterDefinitionId, definition.Id, StringComparison.Ordinal)) continue;
                if (definition.RepeatPolicy == TravelEncounterRepeatPolicy.Repeatable) continue;
                if (definition.RepeatPolicy == TravelEncounterRepeatPolicy.OncePerCondition && string.Equals(encounter.sourceConditionId, conditionId, StringComparison.Ordinal)) return true;
                if (definition.RepeatPolicy == TravelEncounterRepeatPolicy.OncePerJourney && string.Equals(encounter.journeyId, journeyId, StringComparison.Ordinal)) return true;
                if (definition.RepeatPolicy == TravelEncounterRepeatPolicy.OncePerRouteEdge && string.Equals(encounter.target?.StableKey, target?.StableKey, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private int Priority(TravelConditionRecordData record)
        {
            return registry != null && registry.TryGet(record.conditionDefinitionId, out TravelConditionDefinition definition) ? definition.Priority : 0;
        }

        private void RebuildIndexes()
        {
            conditionIdsByTargetKey.Clear();
            foreach (TravelConditionRecordData condition in conditionsById.Values)
            {
                foreach (string key in CandidateKeys(condition.target))
                {
                    AddIndex(conditionIdsByTargetKey, key, condition.conditionId);
                }
            }
        }

        private static IEnumerable<string> CandidateKeys(TravelConditionTargetReferenceData target)
        {
            if (target == null) yield break;
            if (!string.IsNullOrWhiteSpace(target.targetId)) yield return $"{target.scope}:{N(target.targetId)}";
            if (!string.IsNullOrWhiteSpace(target.sourceLocationId)) yield return $"{TravelConditionTargetScope.Location}:{N(target.sourceLocationId)}";
            if (!string.IsNullOrWhiteSpace(target.destinationLocationId)) yield return $"{TravelConditionTargetScope.Location}:{N(target.destinationLocationId)}";
            if (!string.IsNullOrWhiteSpace(target.routeNetworkId)) yield return $"{TravelConditionTargetScope.RouteNetwork}:{N(target.routeNetworkId)}";
            if (!string.IsNullOrWhiteSpace(target.journeyId)) yield return $"{TravelConditionTargetScope.Journey}:{N(target.journeyId)}";
            if (target.traveler != null) yield return $"{TravelConditionTargetScope.Traveler}:{target.traveler.StableKey}";
            if (!string.IsNullOrWhiteSpace(target.targetId) && target.scope != TravelConditionTargetScope.RouteEdge) yield return $"{TravelConditionTargetScope.RouteEdge}:{N(target.targetId)}";
        }

        private bool ValidateTarget(TravelConditionTargetReferenceData target, TravelConditionDefinition definition, long before, out TravelConditionOperationResult failure)
        {
            failure = null;
            if (target == null || target.scope == TravelConditionTargetScope.Unknown) return SetFailure(TravelConditionMutationStatus.MissingTarget, "Travel condition target is required.", before, out failure);
            if (!definition.SupportsScope(target.scope)) return SetFailure(TravelConditionMutationStatus.InvalidRequest, $"Travel condition definition '{definition.Id}' does not support target scope '{target.scope}'.", before, out failure);
            if (target.scope is TravelConditionTargetScope.RouteSegment or TravelConditionTargetScope.RouteEdge && !string.IsNullOrWhiteSpace(target.targetId) && routes != null && !routes.TryGetSegment(target.targetId, out _)) return SetFailure(TravelConditionMutationStatus.MissingTarget, $"Route segment '{target.targetId}' is missing.", before, out failure);
            return true;
        }

        private bool Ready(long before, out TravelConditionOperationResult failure)
        {
            failure = null;
            if (disposed) return SetFailure(TravelConditionMutationStatus.Disposed, "Travel condition runtime is disposed.", before, out failure);
            if (registry == null) return SetFailure(TravelConditionMutationStatus.MissingDefinition, "Definition registry is missing.", before, out failure);
            return true;
        }

        private bool TryGetConditionDefinition(string id, long before, out TravelConditionDefinition definition, out TravelConditionOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry != null && registry.TryGet(N(id), out definition)) return true;
            return SetFailure(TravelConditionMutationStatus.MissingDefinition, $"Travel condition definition '{id}' is missing.", before, out failure);
        }

        private bool TryGetHazardDefinition(string id, long before, out TravelHazardDefinition definition, out TravelConditionOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry != null && registry.TryGet(N(id), out definition)) return true;
            return SetFailure(TravelConditionMutationStatus.MissingDefinition, $"Travel hazard definition '{id}' is missing.", before, out failure);
        }

        private bool TryGetEncounterDefinition(string id, long before, out TravelEncounterDefinition definition, out TravelConditionOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry != null && registry.TryGet(N(id), out definition)) return true;
            return SetFailure(TravelConditionMutationStatus.MissingDefinition, $"Travel encounter definition '{id}' is missing.", before, out failure);
        }

        private bool TryDuplicate(string transactionId, string targetId, string operation, long before, out TravelConditionOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactionsById.TryGetValue(transactionId, out TravelConditionTransactionRecordData transaction)) return false;
            if (!string.Equals(transaction.operation, operation, StringComparison.Ordinal) || !string.Equals(transaction.targetId, targetId, StringComparison.Ordinal)) return false;
            if (conditionsById.TryGetValue(transaction.resultReferenceId, out TravelConditionRecordData condition)) result = TravelConditionOperationResult.Success(new TravelConditionSnapshot(condition), "Duplicate travel condition transaction.", before, before, duplicate: true);
            else if (hazardsById.TryGetValue(transaction.resultReferenceId, out TravelHazardExposureRecordData hazard)) result = TravelConditionOperationResult.HazardSuccess(new TravelHazardExposureSnapshot(hazard), "Duplicate travel hazard transaction.", before, before, duplicate: true);
            else if (encountersById.TryGetValue(transaction.resultReferenceId, out TravelEncounterRecordData encounter)) result = TravelConditionOperationResult.EncounterSuccess(new TravelEncounterSnapshot(encounter), "Duplicate travel encounter transaction.", before, before, duplicate: true);
            return result != null;
        }

        private void Complete(string transactionId, string operation, string targetId, string resultReferenceId)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new TravelConditionTransactionRecordData { transactionId = transactionId, operation = operation, targetId = targetId, resultReferenceId = resultReferenceId, revision = Revision + 1L };
        }

        private void RestoreInternal(TravelConditionRuntimeSaveData saveData)
        {
            saveData ??= new TravelConditionRuntimeSaveData();
            conditionsById.Clear();
            hazardsById.Clear();
            encountersById.Clear();
            transactionsById.Clear();
            foreach (TravelConditionRecordData condition in saveData.conditions ?? Array.Empty<TravelConditionRecordData>()) conditionsById[N(condition.conditionId)] = condition.Clone();
            foreach (TravelHazardExposureRecordData hazard in saveData.hazardExposures ?? Array.Empty<TravelHazardExposureRecordData>()) hazardsById[N(hazard.hazardExposureId)] = hazard.Clone();
            foreach (TravelEncounterRecordData encounter in saveData.encounters ?? Array.Empty<TravelEncounterRecordData>()) encountersById[N(encounter.encounterId)] = encounter.Clone();
            foreach (TravelConditionTransactionRecordData transaction in saveData.transactions ?? Array.Empty<TravelConditionTransactionRecordData>()) transactionsById[N(transaction.transactionId)] = transaction.Clone();
            worldId = N(saveData.worldId);
            Revision = Math.Max(0L, saveData.revision);
            IsDirty = false;
        }

        private static bool IsActiveAt(TravelConditionRecordData condition, double worldTime)
        {
            if (condition.lifecycleState != TravelConditionLifecycleState.Active && condition.lifecycleState != TravelConditionLifecycleState.Scheduled) return false;
            if (condition.lifecycleState == TravelConditionLifecycleState.Scheduled && worldTime < condition.startsWorldTime) return false;
            return condition.endsWorldTime < 0d || worldTime <= condition.endsWorldTime;
        }

        private static bool ValidLifecycle(TravelConditionLifecycleState state) => Enum.IsDefined(typeof(TravelConditionLifecycleState), state) && state != TravelConditionLifecycleState.Unknown && state != TravelConditionLifecycleState.Invalid;
        private static bool IsHidden(TravelConditionVisibility visibility) => visibility == TravelConditionVisibility.Hidden || visibility == TravelConditionVisibility.Secret || visibility == TravelConditionVisibility.Diagnostic;
        private static bool ValidPositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private bool ValidateRevision(long expected, long actual, out TravelConditionOperationResult failure) => expected < 0L || expected == actual ? SetSuccess(out failure) : SetFailure(TravelConditionMutationStatus.RevisionConflict, $"Expected revision {expected}, but current revision is {actual}.", actual, out failure);
        private static bool SetSuccess(out TravelConditionOperationResult failure) { failure = null; return true; }
        private static bool SetFailure(TravelConditionMutationStatus status, string message, long before, out TravelConditionOperationResult failure) { failure = TravelConditionOperationResult.Failure(status, message, before); return false; }
        private TravelConditionOperationResult Fail(TravelConditionMutationStatus status, string message, long before) => TravelConditionOperationResult.Failure(status, message, before);
        private void Touch() { Revision++; IsDirty = true; }
        private static void AddIndex(IDictionary<string, List<string>> index, string key, string id) { if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id)) return; if (!index.TryGetValue(key, out List<string> values)) index[key] = values = new List<string>(); if (!values.Contains(id, StringComparer.Ordinal)) values.Add(id); }
        private static IReadOnlyList<string> GetIds(IDictionary<string, List<string>> index, string key) => index.TryGetValue(key ?? string.Empty, out List<string> ids) ? ids.ToArray() : Array.Empty<string>();
        private static string First(string first, string second) => !string.IsNullOrWhiteSpace(first) ? first.Trim() : N(second);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string StableSuffix(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }
    }
}
