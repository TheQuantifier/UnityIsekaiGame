using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class EntityLocationRuntime : IDisposable
    {
        private readonly Dictionary<string, EntityPlacementRecordData> placementsById = new Dictionary<string, EntityPlacementRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> activePlacementIdByEntityKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> placementIdsByEntityKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> activePlacementIdsByLocationId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> placementIdsByLocationId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntityLocationTransactionRecordData> transactionsById = new Dictionary<string, EntityLocationTransactionRecordData>(StringComparer.Ordinal);
        private readonly HashSet<string> knownEntityKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> inventoryHeldEntityKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntityLocationCapacityRuleData> capacityRulesByLocationId = new Dictionary<string, EntityLocationCapacityRuleData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntityPersonBodyBindingData> activeBodyByPersonId = new Dictionary<string, EntityPersonBodyBindingData>(StringComparer.Ordinal);

        private LocationRuntime locationRuntime;
        private string worldId = PersistenceService.LocalWorldId;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsDisposed => disposed;
        public string WorldId => worldId;
        public int PlacementCount => placementsById.Count;
        public int ActivePlacementCount => activePlacementIdByEntityKey.Count;
        public int KnownEntityCount => knownEntityKeys.Count;
        public int InventoryHeldEntityCount => inventoryHeldEntityKeys.Count;
        public int CapacityRuleCount => capacityRulesByLocationId.Count;

        public IReadOnlyList<EntityPlacementSnapshot> Placements => placementsById.Values
            .OrderBy(record => record.placementId, StringComparer.Ordinal)
            .Select(BuildSnapshot)
            .ToArray();

        public IReadOnlyList<EntityPlacementSnapshot> ActivePlacements => activePlacementIdByEntityKey.Values
            .Where(id => placementsById.ContainsKey(id))
            .Select(id => placementsById[id])
            .OrderBy(record => EntityKey(record.entity), StringComparer.Ordinal)
            .ThenBy(record => record.placementId, StringComparer.Ordinal)
            .Select(BuildSnapshot)
            .ToArray();

        public void Configure(LocationRuntime locations, string world, IEnumerable<EntityLocationReferenceData> knownEntities = null, IEnumerable<EntityLocationCapacityRuleData> capacityRules = null, IEnumerable<EntityPersonBodyBindingData> personBodyBindings = null, IEnumerable<EntityLocationReferenceData> inventoryHeldEntities = null)
        {
            locationRuntime = locations ?? locationRuntime;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            disposed = false;

            if (knownEntities != null)
            {
                knownEntityKeys.Clear();
                foreach (EntityLocationReferenceData entity in knownEntities)
                {
                    RegisterKnownEntity(entity);
                }
            }

            if (capacityRules != null)
            {
                capacityRulesByLocationId.Clear();
                foreach (EntityLocationCapacityRuleData rule in capacityRules)
                {
                    ConfigureCapacity(rule);
                }
            }

            if (personBodyBindings != null)
            {
                activeBodyByPersonId.Clear();
                foreach (EntityPersonBodyBindingData binding in personBodyBindings)
                {
                    RegisterPersonBodyBinding(binding);
                }
            }

            if (inventoryHeldEntities != null)
            {
                inventoryHeldEntityKeys.Clear();
                foreach (EntityLocationReferenceData entity in inventoryHeldEntities)
                {
                    MarkInventoryHeld(entity, true);
                }
            }
        }

        public void RegisterKnownEntity(EntityLocationReferenceData entity)
        {
            string key = EntityKey(entity);
            if (!string.IsNullOrWhiteSpace(key))
            {
                knownEntityKeys.Add(key);
            }
        }

        public void RegisterPersonBodyBinding(EntityPersonBodyBindingData binding)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.personId))
            {
                return;
            }

            activeBodyByPersonId[Normalize(binding.personId)] = binding.Clone();
            RegisterKnownEntity(new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Person, entityId = binding.personId, worldId = worldId });
            if (!string.IsNullOrWhiteSpace(binding.activeBodyId))
            {
                RegisterKnownEntity(new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = binding.activeBodyId, worldId = worldId });
            }
        }

        public bool TryResolvePersonId(EntityLocationReferenceData entity, out string personId)
        {
            personId = string.Empty;
            EntityLocationReferenceData normalized = NormalizeEntity(entity);
            if (normalized == null || string.IsNullOrWhiteSpace(normalized.entityId) || !WorldMatches(normalized.worldId)) return false;
            if (normalized.entityType == LocationOccupantEntityType.Person)
            {
                personId = normalized.entityId;
                return true;
            }

            if (normalized.entityType != LocationOccupantEntityType.Body) return false;
            EntityPersonBodyBindingData binding = activeBodyByPersonId.Values
                .Where(value => value != null && !value.bodyDestroyed && string.Equals(Normalize(value.activeBodyId), normalized.entityId, StringComparison.Ordinal))
                .OrderBy(value => value.personId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (binding == null) return false;
            personId = Normalize(binding.personId);
            return !string.IsNullOrWhiteSpace(personId);
        }

        public void ConfigureCapacity(EntityLocationCapacityRuleData rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.locationId))
            {
                return;
            }

            capacityRulesByLocationId[Normalize(rule.locationId)] = rule.Clone();
        }

        public void MarkInventoryHeld(EntityLocationReferenceData entity, bool held)
        {
            string key = EntityKey(entity);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (held)
            {
                inventoryHeldEntityKeys.Add(key);
            }
            else
            {
                inventoryHeldEntityKeys.Remove(key);
            }
        }

        public EntityLocationOperationResult Place(EntityPlacementRequest request)
        {
            request ??= new EntityPlacementRequest();
            long before = Revision;
            if (disposed) return Fail(EntityLocationOperationStatus.Disposed, "Entity location runtime is disposed.", before);
            if (!ValidateRevision(request.expectedRevision, before, out EntityLocationOperationResult revisionFailure)) return revisionFailure;

            EntityLocationReferenceData entity = NormalizeEntity(request.entity);
            string entityKey = EntityKey(entity);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, entityKey, "place", before, out EntityLocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!ValidateEntity(entity, before, out EntityLocationOperationResult failure)) return failure;
            string locationId = Normalize(request.exactLocationId);
            if (!ValidatePlacementDestination(entity, locationId, before, out failure)) return failure;

            if (activePlacementIdByEntityKey.TryGetValue(entityKey, out string activeId) && placementsById.TryGetValue(activeId, out EntityPlacementRecordData active))
            {
                if (string.Equals(active.exactLocationId, locationId, StringComparison.Ordinal))
                {
                    return EntityLocationOperationResult.Success(BuildSnapshot(active), "Entity already has an active placement at this exact location.", before, before, duplicate: true);
                }

                return Fail(EntityLocationOperationStatus.ConflictingActivePlacement, $"Entity '{entityKey}' already has active placement '{active.placementId}'. Use relocation to move it.", before);
            }

            EntityPlacementRecordData record = CreateRecord(request.placementId, entity, locationId, request.category, request.worldTime, request.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId, string.Empty);
            if (request.preview)
            {
                return EntityLocationOperationResult.Success(BuildSnapshot(record), "Entity placement preview.", before, before, preview: true);
            }

            AddPlacement(record);
            RegisterTransaction(tx, "place", entityKey, record.placementId);
            Revision++;
            IsDirty = true;
            return EntityLocationOperationResult.Success(BuildSnapshot(record), "Entity placed.", before, Revision);
        }

        public EntityLocationOperationResult Relocate(EntityRelocationRequest request)
        {
            request ??= new EntityRelocationRequest();
            long before = Revision;
            if (disposed) return Fail(EntityLocationOperationStatus.Disposed, "Entity location runtime is disposed.", before);
            if (!ValidateRevision(request.expectedRevision, before, out EntityLocationOperationResult revisionFailure)) return revisionFailure;

            EntityLocationReferenceData entity = NormalizeEntity(request.entity);
            string entityKey = EntityKey(entity);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, entityKey, "relocate", before, out EntityLocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!ValidateEntity(entity, before, out EntityLocationOperationResult failure)) return failure;
            if (!activePlacementIdByEntityKey.TryGetValue(entityKey, out string activeId) || !placementsById.TryGetValue(activeId, out EntityPlacementRecordData active))
            {
                return Fail(EntityLocationOperationStatus.MissingPlacement, $"Entity '{entityKey}' has no active placement to relocate.", before);
            }

            string destinationId = Normalize(request.destinationLocationId);
            if (!string.IsNullOrWhiteSpace(request.expectedOriginLocationId) && !string.Equals(active.exactLocationId, Normalize(request.expectedOriginLocationId), StringComparison.Ordinal))
            {
                return Fail(EntityLocationOperationStatus.InvalidRequest, $"Expected origin '{request.expectedOriginLocationId}' does not match active location '{active.exactLocationId}'.", before);
            }

            if (string.Equals(active.exactLocationId, destinationId, StringComparison.Ordinal))
            {
                return EntityLocationOperationResult.Success(BuildSnapshot(active), "Entity is already at destination.", before, before, duplicate: true);
            }

            if (!ValidatePlacementDestination(entity, destinationId, before, out failure)) return failure;

            string transitionId = BuildTransitionId(entityKey, active.exactLocationId, destinationId, placementsById.Count + 1);
            HierarchyTransitionDiff diff = BuildTransitionDiff(active.exactLocationId, destinationId);
            EntityPlacementRecordData ended = active.Clone();
            ended.lifecycleState = EntityPlacementLifecycleState.Superseded;
            ended.endWorldTime = request.worldTime;
            ended.transitionId = transitionId;
            ended.revision++;

            EntityPlacementRecordData next = CreateRecord(request.newPlacementId, entity, destinationId, request.category, request.worldTime, request.visibility, request.sourceEventId, request.sourceRecordId, request.provenanceId, transitionId);
            if (request.preview)
            {
                return EntityLocationOperationResult.Success(BuildSnapshot(next), "Entity relocation preview.", before, before, BuildSnapshot(active), diff, preview: true);
            }

            RemoveActiveIndex(active);
            placementsById[active.placementId] = ended;
            AddPlacement(next);
            RegisterTransaction(tx, "relocate", entityKey, next.placementId);
            Revision++;
            IsDirty = true;
            return EntityLocationOperationResult.Success(BuildSnapshot(next), "Entity relocated.", before, Revision, BuildSnapshot(ended), diff);
        }

        public EntityLocationOperationResult Unplace(EntityUnplacementRequest request)
        {
            request ??= new EntityUnplacementRequest();
            long before = Revision;
            if (disposed) return Fail(EntityLocationOperationStatus.Disposed, "Entity location runtime is disposed.", before);
            if (!ValidateRevision(request.expectedRevision, before, out EntityLocationOperationResult revisionFailure)) return revisionFailure;

            EntityLocationReferenceData entity = NormalizeEntity(request.entity);
            string entityKey = EntityKey(entity);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, entityKey, "unplace", before, out EntityLocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (!ValidateEntity(entity, before, out EntityLocationOperationResult failure)) return failure;
            if (!activePlacementIdByEntityKey.TryGetValue(entityKey, out string activeId) || !placementsById.TryGetValue(activeId, out EntityPlacementRecordData active))
            {
                return Fail(EntityLocationOperationStatus.MissingPlacement, $"Entity '{entityKey}' has no active placement to end.", before);
            }

            EntityPlacementRecordData ended = active.Clone();
            ended.lifecycleState = EntityPlacementLifecycleState.Ended;
            ended.endWorldTime = request.worldTime;
            ended.sourceEventId = FirstNonEmpty(request.sourceEventId, ended.sourceEventId);
            ended.sourceRecordId = FirstNonEmpty(request.sourceRecordId, ended.sourceRecordId);
            ended.provenanceId = FirstNonEmpty(request.provenanceId, ended.provenanceId);
            ended.revision++;

            if (request.preview)
            {
                return EntityLocationOperationResult.Success(BuildSnapshot(ended), "Entity unplacement preview.", before, before, BuildSnapshot(active), preview: true);
            }

            RemoveActiveIndex(active);
            placementsById[active.placementId] = ended;
            RegisterTransaction(tx, "unplace", entityKey, ended.placementId);
            Revision++;
            IsDirty = true;
            return EntityLocationOperationResult.Success(BuildSnapshot(ended), "Entity active placement ended.", before, Revision, BuildSnapshot(active));
        }

        public bool TryGetActivePlacement(EntityLocationReferenceData entity, out EntityPlacementSnapshot snapshot)
        {
            string key = EntityKey(NormalizeEntity(entity));
            if (!string.IsNullOrWhiteSpace(key) && activePlacementIdByEntityKey.TryGetValue(key, out string placementId) && placementsById.TryGetValue(placementId, out EntityPlacementRecordData record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public EntityLocationResolutionResult ResolvePhysicalLocation(EntityLocationReferenceData entity)
        {
            EntityLocationReferenceData normalized = NormalizeEntity(entity);
            if (normalized == null || string.IsNullOrWhiteSpace(normalized.entityId) || normalized.entityType == LocationOccupantEntityType.Unknown)
            {
                return EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.InvalidRequest, "Entity reference is empty.");
            }

            if (!WorldMatches(normalized.worldId))
            {
                return EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.WrongWorld, $"Entity world '{normalized.worldId}' does not match '{worldId}'.");
            }

            if (normalized.entityType == LocationOccupantEntityType.Person && !TryGetActivePlacement(normalized, out _))
            {
                if (!activeBodyByPersonId.TryGetValue(normalized.entityId, out EntityPersonBodyBindingData binding))
                {
                    return EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.MissingBody, $"Person '{normalized.entityId}' has no active body binding.");
                }

                if (binding.bodyDestroyed)
                {
                    return EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.MissingBody, $"Person '{normalized.entityId}' active body is destroyed.");
                }

                EntityLocationReferenceData body = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = binding.activeBodyId, worldId = worldId };
                return TryGetActivePlacement(body, out EntityPlacementSnapshot bodyPlacement)
                    ? EntityLocationResolutionResult.ThroughBody(bodyPlacement)
                    : EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.BodyUnplaced, $"Active body '{binding.activeBodyId}' has no active placement.");
            }

            return TryGetActivePlacement(normalized, out EntityPlacementSnapshot placement)
                ? EntityLocationResolutionResult.Exact(placement)
                : EntityLocationResolutionResult.Failure(EntityPhysicalLocationResolutionStatus.Unplaced, $"Entity '{EntityKey(normalized)}' has no active placement.");
        }

        public LocationOccupancySnapshot GetDirectOccupancy(string locationId, LocationOccupantEntityType entityType = LocationOccupantEntityType.Unknown)
        {
            string id = Normalize(locationId);
            IEnumerable<EntityPlacementSnapshot> placements = activePlacementIdsByLocationId.TryGetValue(id, out List<string> placementIds)
                ? placementIds.Where(value => placementsById.ContainsKey(value)).Select(value => BuildSnapshot(placementsById[value]))
                : Array.Empty<EntityPlacementSnapshot>();
            if (entityType != LocationOccupantEntityType.Unknown)
            {
                placements = placements.Where(placement => placement.EntityType == entityType);
            }

            return new LocationOccupancySnapshot(id, recursive: false, placements);
        }

        public LocationOccupancySnapshot GetRecursiveOccupancy(string locationId, LocationOccupantEntityType entityType = LocationOccupantEntityType.Unknown, bool includeHiddenLocations = true)
        {
            string id = Normalize(locationId);
            HashSet<string> locationIds = new HashSet<string>(StringComparer.Ordinal) { id };
            if (locationRuntime != null)
            {
                foreach (LocationSnapshot descendant in locationRuntime.GetDescendants(id, includeHiddenLocations))
                {
                    locationIds.Add(descendant.LocationId);
                }
            }

            IEnumerable<EntityPlacementSnapshot> placements = locationIds
                .SelectMany(current => GetDirectOccupancy(current, entityType).Placements)
                .OrderBy(placement => placement.EntityKey, StringComparer.Ordinal)
                .ThenBy(placement => placement.PlacementId, StringComparer.Ordinal);
            return new LocationOccupancySnapshot(id, recursive: true, placements);
        }

        public bool IsEntityAt(EntityLocationReferenceData entity, string locationId)
        {
            return ResolvePhysicalLocation(entity).LocationId == Normalize(locationId);
        }

        public bool IsEntityWithin(EntityLocationReferenceData entity, string ancestorLocationId)
        {
            EntityLocationResolutionResult resolved = ResolvePhysicalLocation(entity);
            if (!resolved.Succeeded)
            {
                return false;
            }

            string ancestor = Normalize(ancestorLocationId);
            if (string.Equals(resolved.LocationId, ancestor, StringComparison.Ordinal))
            {
                return true;
            }

            return locationRuntime != null && locationRuntime.GetAncestors(resolved.LocationId).Any(location => location.LocationId == ancestor);
        }

        public bool AreCoLocatedExact(EntityLocationReferenceData first, EntityLocationReferenceData second)
        {
            EntityLocationResolutionResult a = ResolvePhysicalLocation(first);
            EntityLocationResolutionResult b = ResolvePhysicalLocation(second);
            return a.Succeeded && b.Succeeded && string.Equals(a.LocationId, b.LocationId, StringComparison.Ordinal);
        }

        public bool AreCoLocatedWithin(EntityLocationReferenceData first, EntityLocationReferenceData second, string ancestorLocationId)
        {
            return IsEntityWithin(first, ancestorLocationId) && IsEntityWithin(second, ancestorLocationId);
        }

        public EntityPlacementSnapshot GetPlacementAtTime(EntityLocationReferenceData entity, double worldTime)
        {
            string key = EntityKey(NormalizeEntity(entity));
            if (string.IsNullOrWhiteSpace(key) || !placementIdsByEntityKey.TryGetValue(key, out List<string> placementIds))
            {
                return null;
            }

            return placementIds
                .Where(id => placementsById.ContainsKey(id))
                .Select(id => placementsById[id])
                .Where(record => record.startWorldTime <= worldTime && (record.endWorldTime < 0d || record.endWorldTime > worldTime))
                .OrderByDescending(record => record.startWorldTime)
                .ThenBy(record => record.placementId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .FirstOrDefault();
        }

        public EntityPlacementSnapshot GetLastKnownPlacement(EntityLocationReferenceData entity)
        {
            string key = EntityKey(NormalizeEntity(entity));
            if (string.IsNullOrWhiteSpace(key) || !placementIdsByEntityKey.TryGetValue(key, out List<string> placementIds))
            {
                return null;
            }

            return placementIds
                .Where(id => placementsById.ContainsKey(id))
                .Select(id => placementsById[id])
                .OrderByDescending(record => record.endWorldTime < 0d ? record.startWorldTime : record.endWorldTime)
                .ThenByDescending(record => record.startWorldTime)
                .ThenBy(record => record.placementId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .FirstOrDefault();
        }

        public HierarchyTransitionDiff BuildTransitionDiff(string originLocationId, string destinationLocationId)
        {
            string origin = Normalize(originLocationId);
            string destination = Normalize(destinationLocationId);
            string[] originPath = PathIds(origin);
            string[] destinationPath = PathIds(destination);
            HashSet<string> originSet = new HashSet<string>(originPath, StringComparer.Ordinal);
            HashSet<string> destinationSet = new HashSet<string>(destinationPath, StringComparer.Ordinal);
            return new HierarchyTransitionDiff(
                destinationSet.Except(originSet, StringComparer.Ordinal),
                originSet.Except(destinationSet, StringComparer.Ordinal),
                destinationSet.Intersect(originSet, StringComparer.Ordinal),
                destinationPath);
        }

        public EntityLocationRuntimeSaveData CreateSaveData()
        {
            return new EntityLocationRuntimeSaveData
            {
                schemaVersion = EntityLocationRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                placements = placementsById.Values.OrderBy(record => record.placementId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(record => record.transactionId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                knownEntities = knownEntityKeys.OrderBy(key => key, StringComparer.Ordinal).Select(ParseKey).Where(value => value != null).ToList(),
                inventoryHeldEntities = inventoryHeldEntityKeys.OrderBy(key => key, StringComparer.Ordinal).Select(ParseKey).Where(value => value != null).ToList(),
                capacityRules = capacityRulesByLocationId.Values.OrderBy(rule => rule.locationId, StringComparer.Ordinal).Select(rule => rule.Clone()).ToList(),
                personBodyBindings = activeBodyByPersonId.Values.OrderBy(binding => binding.personId, StringComparer.Ordinal).Select(binding => binding.Clone()).ToList()
            };
        }

        public EntityLocationOperationResult RestoreFromSaveData(EntityLocationRuntimeSaveData saveData, LocationRuntime locations, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, locations ?? locationRuntime, expectedWorldId, out string failure))
            {
                return Fail(EntityLocationOperationStatus.PersistenceInvalid, failure, before);
            }

            EntityLocationRuntimeSaveData rollback = CreateSaveData();
            LocationRuntime priorLocations = locationRuntime;
            try
            {
                RestoreInternal(saveData ?? new EntityLocationRuntimeSaveData());
                locationRuntime = locations ?? priorLocations;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                IsDirty = !restoring;
                return EntityLocationOperationResult.Success(null, "Entity locations restored.", before, Revision);
            }
            catch (Exception exception)
            {
                RestoreInternal(rollback);
                locationRuntime = priorLocations;
                return Fail(EntityLocationOperationStatus.RestoreFailed, $"Entity location restore failed: {exception.Message}", before);
            }
        }

        public bool ValidateRuntime(out string failure)
        {
            return ValidateSaveData(CreateSaveData(), locationRuntime, worldId, out failure);
        }

        public static bool ValidateSaveData(EntityLocationRuntimeSaveData saveData, LocationRuntime locations, string expectedWorldId, out string failure)
        {
            List<string> errors = new List<string>();
            saveData ??= new EntityLocationRuntimeSaveData();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData.schemaVersion < 1 || saveData.schemaVersion > EntityLocationRuntimeSaveData.CurrentSchemaVersion) errors.Add($"Unsupported entity location save schema {saveData.schemaVersion}.");
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), world, StringComparison.Ordinal)) errors.Add($"Entity location save world '{saveData.worldId}' does not match expected world '{world}'.");

            HashSet<string> known = new HashSet<string>((saveData.knownEntities ?? new List<EntityLocationReferenceData>()).Select(EntityKey).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> inventory = new HashSet<string>((saveData.inventoryHeldEntities ?? new List<EntityLocationReferenceData>()).Select(EntityKey).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> placementIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeEntities = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (EntityPlacementRecordData record in saveData.placements ?? new List<EntityPlacementRecordData>())
            {
                if (record == null)
                {
                    errors.Add("Entity location save contains a null placement.");
                    continue;
                }

                string placementId = Normalize(record.placementId);
                string entityKey = EntityKey(record.entity);
                string locationId = Normalize(record.exactLocationId);
                if (string.IsNullOrWhiteSpace(placementId)) errors.Add("Entity placement is missing a placement ID.");
                else if (!placementIds.Add(placementId)) errors.Add($"Duplicate entity placement ID '{placementId}'.");
                if (string.IsNullOrWhiteSpace(entityKey)) errors.Add($"Entity placement '{placementId}' has an invalid entity reference.");
                if (known.Count > 0 && !known.Contains(entityKey)) errors.Add($"Entity placement '{placementId}' references unknown entity '{entityKey}'.");
                if (inventory.Contains(entityKey) && record.lifecycleState == EntityPlacementLifecycleState.Active) errors.Add($"Entity placement '{placementId}' places inventory-held entity '{entityKey}' in the world.");
                if (!string.IsNullOrWhiteSpace(record.worldId) && !string.Equals(record.worldId.Trim(), world, StringComparison.Ordinal)) errors.Add($"Entity placement '{placementId}' belongs to world '{record.worldId}', not '{world}'.");
                if (string.IsNullOrWhiteSpace(locationId)) errors.Add($"Entity placement '{placementId}' has no exact location.");
                else if (locations == null || !locations.TryGetSnapshot(locationId, out LocationSnapshot location)) errors.Add($"Entity placement '{placementId}' references missing location '{locationId}'.");
                else if (record.lifecycleState == EntityPlacementLifecycleState.Active && CannotRetainExistingPlacement(location.LifecycleState)) errors.Add($"Entity placement '{placementId}' is active in unavailable location '{locationId}' ({location.LifecycleState}).");
                if (!Enum.IsDefined(typeof(LocationOccupantEntityType), record.entity?.entityType ?? LocationOccupantEntityType.Unknown) || record.entity.entityType == LocationOccupantEntityType.Unknown) errors.Add($"Entity placement '{placementId}' has invalid entity type '{record.entity?.entityType}'.");
                if (!Enum.IsDefined(typeof(EntityPlacementCategory), record.category) || record.category == EntityPlacementCategory.Unknown) errors.Add($"Entity placement '{placementId}' has invalid category '{record.category}'.");
                if (!Enum.IsDefined(typeof(EntityPlacementLifecycleState), record.lifecycleState) || record.lifecycleState == EntityPlacementLifecycleState.Unknown) errors.Add($"Entity placement '{placementId}' has invalid lifecycle '{record.lifecycleState}'.");
                if (record.lifecycleState == EntityPlacementLifecycleState.Active && !activeEntities.Add(entityKey)) errors.Add($"Entity '{entityKey}' has more than one active exact placement.");
                if (record.lifecycleState != EntityPlacementLifecycleState.Active && record.endWorldTime < 0d) errors.Add($"Ended entity placement '{placementId}' has no end time.");
                if (record.endWorldTime >= 0d && record.endWorldTime < record.startWorldTime) errors.Add($"Entity placement '{placementId}' ends before it starts.");
            }

            foreach (EntityLocationTransactionRecordData tx in saveData.transactions ?? new List<EntityLocationTransactionRecordData>())
            {
                if (tx == null) continue;
                string txId = Normalize(tx.transactionId);
                if (string.IsNullOrWhiteSpace(txId)) errors.Add("Entity location transaction has no ID.");
                else if (!transactionIds.Add(txId)) errors.Add($"Duplicate entity location transaction '{txId}'.");
                if (!string.IsNullOrWhiteSpace(tx.placementId) && !placementIds.Contains(Normalize(tx.placementId))) errors.Add($"Entity location transaction '{txId}' references missing placement '{tx.placementId}'.");
            }

            foreach (EntityLocationCapacityRuleData rule in saveData.capacityRules ?? new List<EntityLocationCapacityRuleData>())
            {
                if (rule == null) continue;
                string locationId = Normalize(rule.locationId);
                if (string.IsNullOrWhiteSpace(locationId)) errors.Add("Entity location capacity rule has no location.");
                else if (locations == null || !locations.TryGetSnapshot(locationId, out _)) errors.Add($"Entity location capacity rule references missing location '{locationId}'.");
                if (rule.maxDirectOccupants == 0 || rule.maxDirectOccupants < -1) errors.Add($"Entity location capacity rule for '{locationId}' has invalid maxDirectOccupants '{rule.maxDirectOccupants}'.");
                foreach (LocationOccupantEntityType type in rule.allowedEntityTypes ?? Array.Empty<LocationOccupantEntityType>())
                {
                    if (!Enum.IsDefined(typeof(LocationOccupantEntityType), type) || type == LocationOccupantEntityType.Unknown) errors.Add($"Entity location capacity rule for '{locationId}' has invalid allowed type '{type}'.");
                }
            }

            foreach (EntityPersonBodyBindingData binding in saveData.personBodyBindings ?? new List<EntityPersonBodyBindingData>())
            {
                if (binding == null) continue;
                if (string.IsNullOrWhiteSpace(binding.personId)) errors.Add("Person-body binding has no person ID.");
                if (!binding.bodyDestroyed && string.IsNullOrWhiteSpace(binding.activeBodyId)) errors.Add($"Person-body binding for '{binding.personId}' has no active body ID.");
                if (known.Count > 0 && !known.Contains(EntityLocationReferenceKey.Build(LocationOccupantEntityType.Person, binding.personId, world))) errors.Add($"Person-body binding references unknown person '{binding.personId}'.");
                if (known.Count > 0 && !string.IsNullOrWhiteSpace(binding.activeBodyId) && !known.Contains(EntityLocationReferenceKey.Build(LocationOccupantEntityType.Body, binding.activeBodyId, world))) errors.Add($"Person-body binding references unknown body '{binding.activeBodyId}'.");
            }

            failure = errors.Count == 0
                ? "Entity location validation succeeded."
                : $"Entity location validation failed with {errors.Count} error(s): {string.Join(" | ", errors)}";
            return errors.Count == 0;
        }

        public void Reset()
        {
            placementsById.Clear();
            activePlacementIdByEntityKey.Clear();
            placementIdsByEntityKey.Clear();
            activePlacementIdsByLocationId.Clear();
            placementIdsByLocationId.Clear();
            transactionsById.Clear();
            knownEntityKeys.Clear();
            inventoryHeldEntityKeys.Clear();
            capacityRulesByLocationId.Clear();
            activeBodyByPersonId.Clear();
            Revision = 0L;
            IsDirty = false;
            disposed = false;
        }

        public void Dispose()
        {
            Reset();
            disposed = true;
        }

        private bool ValidateRevision(long expectedRevision, long before, out EntityLocationOperationResult failure)
        {
            if (expectedRevision >= 0L && expectedRevision != Revision)
            {
                failure = Fail(EntityLocationOperationStatus.RevisionConflict, $"Expected entity location revision {expectedRevision}, but current revision is {Revision}.", before);
                return false;
            }

            failure = null;
            return true;
        }

        private bool ValidateEntity(EntityLocationReferenceData entity, long before, out EntityLocationOperationResult failure)
        {
            failure = null;
            string key = EntityKey(entity);
            if (entity == null || string.IsNullOrWhiteSpace(entity.entityId) || entity.entityType == LocationOccupantEntityType.Unknown)
            {
                failure = Fail(EntityLocationOperationStatus.InvalidRequest, "Entity placement requires a concrete entity reference.", before);
                return false;
            }

            if (!WorldMatches(entity.worldId))
            {
                failure = Fail(EntityLocationOperationStatus.WrongWorld, $"Entity world '{entity.worldId}' does not match '{worldId}'.", before);
                return false;
            }

            if (knownEntityKeys.Count > 0 && !knownEntityKeys.Contains(key))
            {
                failure = Fail(EntityLocationOperationStatus.MissingEntity, $"Entity '{key}' is not known to the entity location runtime.", before);
                return false;
            }

            if (inventoryHeldEntityKeys.Contains(key))
            {
                failure = Fail(EntityLocationOperationStatus.InventoryConflict, $"Entity '{key}' is held by inventory/custody and cannot also have an active exact world placement.", before);
                return false;
            }

            return true;
        }

        private bool ValidatePlacementDestination(EntityLocationReferenceData entity, string locationId, long before, out EntityLocationOperationResult failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                failure = Fail(EntityLocationOperationStatus.InvalidRequest, "Entity placement requires an exact location ID.", before);
                return false;
            }

            if (locationRuntime == null || !locationRuntime.TryGetSnapshot(locationId, out LocationSnapshot location))
            {
                failure = Fail(EntityLocationOperationStatus.MissingLocation, $"Location '{locationId}' does not exist.", before);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(location.WorldId) && !string.Equals(location.WorldId, worldId, StringComparison.Ordinal))
            {
                failure = Fail(EntityLocationOperationStatus.WrongWorld, $"Location '{locationId}' belongs to world '{location.WorldId}', not '{worldId}'.", before);
                return false;
            }

            if (!CanAcceptNewPlacement(location.LifecycleState))
            {
                failure = Fail(EntityLocationOperationStatus.InactiveLocation, $"Location '{locationId}' cannot accept new ordinary placements while {location.LifecycleState}.", before);
                return false;
            }

            if (capacityRulesByLocationId.TryGetValue(locationId, out EntityLocationCapacityRuleData rule))
            {
                if ((rule.allowedEntityTypes ?? Array.Empty<LocationOccupantEntityType>()).Length > 0 && !rule.allowedEntityTypes.Contains(entity.entityType))
                {
                    failure = Fail(EntityLocationOperationStatus.OccupantTypeNotAllowed, $"Location '{locationId}' does not allow occupant type '{entity.entityType}'.", before);
                    return false;
                }

                if (rule.maxDirectOccupants > 0 && GetDirectOccupancy(locationId).Count >= rule.maxDirectOccupants)
                {
                    failure = Fail(EntityLocationOperationStatus.CapacityFull, $"Location '{locationId}' is at direct occupant capacity {rule.maxDirectOccupants}.", before);
                    return false;
                }
            }

            return true;
        }

        private EntityPlacementRecordData CreateRecord(string placementId, EntityLocationReferenceData entity, string locationId, EntityPlacementCategory category, double worldTime, LocationVisibility visibility, string sourceEventId, string sourceRecordId, string provenanceId, string transitionId)
        {
            string id = string.IsNullOrWhiteSpace(placementId)
                ? BuildPlacementId(entity, locationId, placementsById.Count + 1)
                : Normalize(placementId);
            return new EntityPlacementRecordData
            {
                placementId = id,
                entity = NormalizeEntity(entity),
                exactLocationId = Normalize(locationId),
                worldId = worldId,
                category = category == EntityPlacementCategory.Unknown ? EntityPlacementCategory.Present : category,
                lifecycleState = EntityPlacementLifecycleState.Active,
                startWorldTime = worldTime,
                endWorldTime = -1d,
                visibility = visibility,
                sourceEventId = Normalize(sourceEventId),
                sourceRecordId = Normalize(sourceRecordId),
                provenanceId = Normalize(provenanceId),
                transitionId = Normalize(transitionId),
                revision = 1L
            };
        }

        private void AddPlacement(EntityPlacementRecordData record)
        {
            string placementId = Normalize(record.placementId);
            string entityKey = EntityKey(record.entity);
            string locationId = Normalize(record.exactLocationId);
            placementsById[placementId] = record;
            AddToIndex(placementIdsByEntityKey, entityKey, placementId);
            AddToIndex(placementIdsByLocationId, locationId, placementId);
            if (record.lifecycleState == EntityPlacementLifecycleState.Active)
            {
                activePlacementIdByEntityKey[entityKey] = placementId;
                AddToIndex(activePlacementIdsByLocationId, locationId, placementId);
            }
        }

        private void RemoveActiveIndex(EntityPlacementRecordData record)
        {
            string entityKey = EntityKey(record.entity);
            string locationId = Normalize(record.exactLocationId);
            activePlacementIdByEntityKey.Remove(entityKey);
            if (activePlacementIdsByLocationId.TryGetValue(locationId, out List<string> ids))
            {
                ids.Remove(record.placementId);
                if (ids.Count == 0)
                {
                    activePlacementIdsByLocationId.Remove(locationId);
                }
            }
        }

        private void RestoreInternal(EntityLocationRuntimeSaveData saveData)
        {
            Reset();
            worldId = string.IsNullOrWhiteSpace(saveData.worldId) ? worldId : saveData.worldId.Trim();
            foreach (EntityLocationReferenceData entity in saveData.knownEntities ?? new List<EntityLocationReferenceData>()) RegisterKnownEntity(entity);
            foreach (EntityLocationReferenceData entity in saveData.inventoryHeldEntities ?? new List<EntityLocationReferenceData>()) MarkInventoryHeld(entity, true);
            foreach (EntityLocationCapacityRuleData rule in saveData.capacityRules ?? new List<EntityLocationCapacityRuleData>()) ConfigureCapacity(rule);
            foreach (EntityPersonBodyBindingData binding in saveData.personBodyBindings ?? new List<EntityPersonBodyBindingData>()) RegisterPersonBodyBinding(binding);
            foreach (EntityLocationTransactionRecordData tx in saveData.transactions ?? new List<EntityLocationTransactionRecordData>()) transactionsById[Normalize(tx.transactionId)] = tx.Clone();
            foreach (EntityPlacementRecordData placement in saveData.placements ?? new List<EntityPlacementRecordData>())
            {
                AddPlacement(placement.Clone());
            }

            Revision = Math.Max(0L, saveData.revision);
            IsDirty = false;
        }

        private bool TryDuplicate(string transactionId, string entityKey, string operation, long before, out EntityLocationOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!transactionsById.TryGetValue(transactionId, out EntityLocationTransactionRecordData tx))
            {
                return false;
            }

            if (!string.Equals(tx.operation, operation, StringComparison.Ordinal) || !string.Equals(tx.entityKey, entityKey, StringComparison.Ordinal))
            {
                result = Fail(EntityLocationOperationStatus.InvalidRequest, $"Transaction '{transactionId}' already exists for a different entity location operation.", before);
                return true;
            }

            EntityPlacementSnapshot placement = !string.IsNullOrWhiteSpace(tx.placementId) && placementsById.TryGetValue(tx.placementId, out EntityPlacementRecordData record)
                ? BuildSnapshot(record)
                : null;
            result = EntityLocationOperationResult.Success(placement, "Duplicate entity location transaction ignored.", before, before, duplicate: true);
            return true;
        }

        private void RegisterTransaction(string transactionId, string operation, string entityKey, string placementId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            transactionsById[Normalize(transactionId)] = new EntityLocationTransactionRecordData
            {
                transactionId = Normalize(transactionId),
                operation = operation ?? string.Empty,
                entityKey = entityKey ?? string.Empty,
                placementId = placementId ?? string.Empty,
                revision = Revision + 1L
            };
        }

        private string[] PathIds(string locationId)
        {
            if (locationRuntime == null || string.IsNullOrWhiteSpace(locationId))
            {
                return string.IsNullOrWhiteSpace(locationId) ? Array.Empty<string>() : new[] { Normalize(locationId) };
            }

            LocationHierarchyPathResult path = locationRuntime.GetHierarchyPath(locationId);
            return path.Path.Select(location => location.LocationId).ToArray();
        }

        private bool WorldMatches(string candidateWorldId)
        {
            return string.IsNullOrWhiteSpace(candidateWorldId) || string.Equals(Normalize(candidateWorldId), worldId, StringComparison.Ordinal);
        }

        private static bool CanAcceptNewPlacement(LocationLifecycleState state)
        {
            return state == LocationLifecycleState.Active;
        }

        private static bool CannotRetainExistingPlacement(LocationLifecycleState state)
        {
            return state == LocationLifecycleState.Unknown
                || state == LocationLifecycleState.Proposed
                || state == LocationLifecycleState.Destroyed
                || state == LocationLifecycleState.Removed
                || state == LocationLifecycleState.Historical;
        }

        private EntityLocationOperationResult Fail(EntityLocationOperationStatus status, string message, long before)
        {
            return EntityLocationOperationResult.Failure(status, message, before);
        }

        private static EntityPlacementSnapshot BuildSnapshot(EntityPlacementRecordData record)
        {
            return new EntityPlacementSnapshot(record);
        }

        private static EntityLocationReferenceData NormalizeEntity(EntityLocationReferenceData entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new EntityLocationReferenceData
            {
                entityType = entity.entityType,
                entityId = Normalize(entity.entityId),
                worldId = Normalize(entity.worldId)
            };
        }

        private static string EntityKey(EntityLocationReferenceData entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            return EntityLocationReferenceKey.Build(entity.entityType, entity.entityId, entity.worldId);
        }

        private static EntityLocationReferenceData ParseKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string[] parts = key.Split(':');
            if (parts.Length < 3 || !Enum.TryParse(parts[0], out LocationOccupantEntityType type))
            {
                return null;
            }

            return new EntityLocationReferenceData { entityType = type, worldId = parts[1], entityId = string.Join(":", parts.Skip(2)) };
        }

        private static void AddToIndex(Dictionary<string, List<string>> index, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<string> values))
            {
                values = new List<string>();
                index[key] = values;
            }

            if (!values.Contains(value))
            {
                values.Add(value);
                values.Sort(StringComparer.Ordinal);
            }
        }

        private static string BuildPlacementId(EntityLocationReferenceData entity, string locationId, int sequence)
        {
            return $"placement.{Normalize(entity?.entityType.ToString()).ToLowerInvariant()}.{Sanitize(entity?.entityId)}.{Sanitize(locationId)}.{sequence:0000}";
        }

        private static string BuildTransitionId(string entityKey, string origin, string destination, int sequence)
        {
            return $"entity-location-transition.{Sanitize(entityKey)}.{Sanitize(origin)}.{Sanitize(destination)}.{sequence:0000}";
        }

        private static string Sanitize(string value)
        {
            string normalized = Normalize(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "none";
            }

            char[] chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '-' && chars[i] != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? Array.Empty<string>()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
