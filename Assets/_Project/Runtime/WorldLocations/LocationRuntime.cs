using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class LocationRuntime : IDisposable
    {
        public const int MaxContainmentDepth = 32;

        private readonly Dictionary<string, LocationRecordData> recordsById = new Dictionary<string, LocationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationNameRecordData> namesById = new Dictionary<string, LocationNameRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationTransactionRecordData> transactionsById = new Dictionary<string, LocationTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationContainmentLinkData> containmentLinksById = new Dictionary<string, LocationContainmentLinkData>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationSpatialRelationshipData> spatialRelationshipsById = new Dictionary<string, LocationSpatialRelationshipData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId = PersistenceService.LocalWorldId;
        private HashSet<string> knownPropertyIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownGovernmentIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownTerritoryIds = new HashSet<string>(StringComparer.Ordinal);
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => registry != null && !string.IsNullOrWhiteSpace(worldId) && !disposed;
        public bool IsDisposed => disposed;
        public int Count => recordsById.Count;
        public int ContainmentLinkCount => containmentLinksById.Count;
        public int SpatialRelationshipCount => spatialRelationshipsById.Count;
        public string WorldId => worldId;
        public IReadOnlyList<LocationSnapshot> Snapshots => recordsById.Values.OrderBy(record => record.locationId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        public IReadOnlyList<LocationContainmentSnapshot> ContainmentLinks => containmentLinksById.Values.OrderBy(link => link.linkId, StringComparer.Ordinal).Select(BuildContainmentSnapshot).ToArray();
        public IReadOnlyList<LocationSpatialRelationshipSnapshot> SpatialRelationships => spatialRelationshipsById.Values.OrderBy(relationship => relationship.relationshipId, StringComparer.Ordinal).Select(BuildSpatialSnapshot).ToArray();

        public void Configure(
            DefinitionRegistry definitionRegistry,
            string world,
            IEnumerable<string> knownProperties = null,
            IEnumerable<string> knownOrganizations = null,
            IEnumerable<string> knownGovernments = null,
            IEnumerable<string> knownTerritories = null)
        {
            registry = definitionRegistry ?? registry;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            knownPropertyIds = new HashSet<string>(Clean(knownProperties), StringComparer.Ordinal);
            knownOrganizationIds = new HashSet<string>(Clean(knownOrganizations), StringComparer.Ordinal);
            knownGovernmentIds = new HashSet<string>(Clean(knownGovernments), StringComparer.Ordinal);
            knownTerritoryIds = new HashSet<string>(Clean(knownTerritories), StringComparer.Ordinal);
            disposed = false;
        }

        public LocationOperationResult CreateLocation(LocationCreateRequest request)
        {
            request ??= new LocationCreateRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string locationId = Normalize(request.locationId);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, locationId, "create", before, out LocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (request.expectedRevision >= 0L && request.expectedRevision != Revision)
            {
                return Fail(LocationOperationStatus.RevisionConflict, $"Expected location runtime revision {request.expectedRevision}, but current revision is {Revision}.", before);
            }

            if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(request.locationDefinitionId))
            {
                return Fail(LocationOperationStatus.InvalidRequest, "Location ID and definition ID are required.", before);
            }

            if (recordsById.TryGetValue(locationId, out LocationRecordData existing))
            {
                if (string.Equals(existing.locationDefinitionId, Normalize(request.locationDefinitionId), StringComparison.Ordinal)
                    && string.Equals(existing.officialName, NormalizeName(request.officialName), StringComparison.Ordinal))
                {
                    return LocationOperationResult.Success(BuildSnapshot(existing), "Location already exists.", before, before, duplicate: true);
                }

                return Fail(LocationOperationStatus.DuplicateLocationId, $"Location '{locationId}' already exists with different data.", before);
            }

            if (!TryGetDefinition(request.locationDefinitionId, out LocationDefinition definition, before, out LocationOperationResult failure))
            {
                return failure;
            }

            string authoredPlaceDefinitionId = Normalize(request.authoredPlaceDefinitionId);
            if (!string.IsNullOrWhiteSpace(authoredPlaceDefinitionId)
                && RegistryContainsAuthoredPlaces(registry)
                && !registry.TryGet(authoredPlaceDefinitionId, out PlaceDefinition _))
            {
                return Fail(LocationOperationStatus.MissingDefinition, $"Location references missing authored Place Definition '{authoredPlaceDefinitionId}'.", before);
            }

            LocationLifecycleState lifecycle = request.initialLifecycleState == LocationLifecycleState.Unknown ? LocationLifecycleState.Active : request.initialLifecycleState;
            if (!CanCreateLifecycle(lifecycle))
            {
                return Fail(LocationOperationStatus.InvalidLifecycleTransition, "Locations must be created proposed, active, inactive, closed, or historical.", before);
            }

            string officialName = NormalizeName(request.officialName);
            if (string.IsNullOrWhiteSpace(officialName))
            {
                return Fail(LocationOperationStatus.InvalidName, "Location official name is required.", before);
            }

            if (!definition.SupportsVisibility(request.visibility))
            {
                return Fail(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not support visibility '{request.visibility}'.", before);
            }

            string[] tags = Clean(request.semanticTagIds);
            foreach (string tag in tags)
            {
                if (!definition.AllowsSemanticTag(tag))
                {
                    return Fail(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not allow semantic tag '{tag}'.", before);
                }
            }

            if (!ValidateAssociations(definition, request.associatedPropertyId, request.associatedOrganizationId, request.associatedGovernmentId, request.associatedTerritoryIds, request.associations, before, out failure))
            {
                return failure;
            }

            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(CreateRecord(request, definition, locationId, officialName, tags)), "Location create preview.", before, before, preview: true);
            }

            LocationRecordData record = CreateRecord(request, definition, locationId, officialName, tags);
            recordsById.Add(record.locationId, record);
            namesById.Add(record.currentOfficialNameRecordId, new LocationNameRecordData
            {
                nameRecordId = record.currentOfficialNameRecordId,
                locationId = record.locationId,
                category = LocationNameCategory.Official,
                value = record.officialName,
                effectiveStartWorldTime = request.createdWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            });

            RegisterTransaction(tx, "create", record.locationId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(record), "Location created.", before, Revision);
        }

        public LocationOperationResult RenameLocation(LocationRenameRequest request)
        {
            request ??= new LocationRenameRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string locationId = Normalize(request.locationId);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, locationId, "rename", before, out LocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (request.expectedRevision >= 0L && request.expectedRevision != Revision)
            {
                return Fail(LocationOperationStatus.RevisionConflict, $"Expected location runtime revision {request.expectedRevision}, but current revision is {Revision}.", before);
            }

            if (!recordsById.TryGetValue(locationId, out LocationRecordData record))
            {
                return Fail(LocationOperationStatus.MissingLocation, $"Location '{locationId}' does not exist.", before);
            }

            string newName = NormalizeName(request.newName);
            if (string.IsNullOrWhiteSpace(newName))
            {
                return Fail(LocationOperationStatus.InvalidName, "Location name is required.", before);
            }

            if (request.preview)
            {
                LocationRecordData preview = record.Clone();
                ApplyName(preview, request.category, newName);
                return LocationOperationResult.Success(BuildSnapshot(preview), "Location rename preview.", before, before, preview: true);
            }

            LocationNameRecordData previousOfficial = string.IsNullOrWhiteSpace(record.currentOfficialNameRecordId) || !namesById.TryGetValue(record.currentOfficialNameRecordId, out LocationNameRecordData previous) ? null : previous;
            if (request.category == LocationNameCategory.Official && previousOfficial != null)
            {
                previousOfficial.effectiveEndWorldTime = request.effectiveWorldTime;
                previousOfficial.revision++;
            }

            string nameId = BuildNameId(locationId, request.category.ToString().ToLowerInvariant(), namesById.Count + 1);
            namesById[nameId] = new LocationNameRecordData
            {
                nameRecordId = nameId,
                locationId = locationId,
                category = request.category,
                value = newName,
                effectiveStartWorldTime = request.effectiveWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };

            ApplyName(record, request.category, newName);
            if (request.category == LocationNameCategory.Official)
            {
                record.currentOfficialNameRecordId = nameId;
            }

            record.revision++;
            RegisterTransaction(tx, "rename", locationId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(record), "Location renamed.", before, Revision);
        }

        public LocationOperationResult TransitionLifecycle(LocationLifecycleTransitionRequest request)
        {
            request ??= new LocationLifecycleTransitionRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string locationId = Normalize(request.locationId);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, locationId, "lifecycle", before, out LocationOperationResult duplicate))
            {
                return duplicate;
            }

            if (request.expectedRevision >= 0L && request.expectedRevision != Revision)
            {
                return Fail(LocationOperationStatus.RevisionConflict, $"Expected location runtime revision {request.expectedRevision}, but current revision is {Revision}.", before);
            }

            if (!recordsById.TryGetValue(locationId, out LocationRecordData record))
            {
                return Fail(LocationOperationStatus.MissingLocation, $"Location '{locationId}' does not exist.", before);
            }

            if (!CanTransition(record.lifecycleState, request.targetState))
            {
                return Fail(LocationOperationStatus.InvalidLifecycleTransition, $"Cannot transition location from '{record.lifecycleState}' to '{request.targetState}'.", before);
            }

            if (record.lifecycleState == request.targetState)
            {
                return LocationOperationResult.Success(BuildSnapshot(record), "Location lifecycle already matches requested state.", before, before, duplicate: true);
            }

            if (request.preview)
            {
                LocationRecordData preview = record.Clone();
                ApplyLifecycle(preview, request.targetState, request.worldTime);
                return LocationOperationResult.Success(BuildSnapshot(preview), "Location lifecycle preview.", before, before, preview: true);
            }

            ApplyLifecycle(record, request.targetState, request.worldTime);
            record.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? record.sourceEventId : Normalize(request.sourceEventId);
            record.sourceRecordId = string.IsNullOrWhiteSpace(request.sourceRecordId) ? record.sourceRecordId : Normalize(request.sourceRecordId);
            record.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? record.provenanceId : Normalize(request.provenanceId);
            record.revision++;
            RegisterTransaction(tx, "lifecycle", locationId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(record), "Location lifecycle updated.", before, Revision);
        }

        public LocationOperationResult AssignContainment(LocationContainmentRequest request)
        {
            request ??= new LocationContainmentRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string parentId = Normalize(request.parentLocationId);
            string childId = Normalize(request.childLocationId);
            string linkId = Normalize(request.linkId);
            if (string.IsNullOrWhiteSpace(linkId)) linkId = BuildContainmentLinkId(parentId, childId, request.kind);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, linkId, "containment.assign", before, out LocationOperationResult duplicate)) return duplicate;
            if (!ValidateExpectedRevision(request.expectedRevision, before, out LocationOperationResult failure)) return failure;
            if (!ValidateContainmentRequest(parentId, childId, request.kind, before, out failure)) return failure;

            LocationContainmentLinkData active = FindActiveParentLink(childId);
            if (active != null)
            {
                if (string.Equals(active.parentLocationId, parentId, StringComparison.Ordinal) && active.kind == request.kind)
                {
                    return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location containment already matches requested parent.", before, before, duplicate: true);
                }

                return Fail(LocationOperationStatus.ActiveParentConflict, $"Location '{childId}' already has active parent '{active.parentLocationId}'. Use reparenting for atomic parent changes.", before);
            }

            if (WouldCreateCycle(parentId, childId, null, out string cyclePath))
            {
                return Fail(LocationOperationStatus.CycleDetected, $"Containment would create a cycle: {cyclePath}.", before);
            }

            if (GetDepth(parentId, null) + 1 > MaxContainmentDepth)
            {
                return Fail(LocationOperationStatus.DepthLimitExceeded, $"Containment depth would exceed {MaxContainmentDepth}.", before);
            }

            LocationContainmentLinkData link = CreateContainmentLink(linkId, parentId, childId, request);
            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location containment preview.", before, before, preview: true);
            }

            containmentLinksById.Add(link.linkId, link);
            RegisterTransaction(tx, "containment.assign", link.linkId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location containment assigned.", before, Revision);
        }

        public LocationOperationResult ReparentLocation(LocationReparentRequest request)
        {
            request ??= new LocationReparentRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string childId = Normalize(request.childLocationId);
            string oldParentId = Normalize(request.oldParentLocationId);
            string newParentId = Normalize(request.newParentLocationId);
            string linkId = Normalize(request.newLinkId);
            if (string.IsNullOrWhiteSpace(linkId)) linkId = BuildContainmentLinkId(newParentId, childId, request.kind);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, linkId, "containment.reparent", before, out LocationOperationResult duplicate)) return duplicate;
            if (!ValidateExpectedRevision(request.expectedRevision, before, out LocationOperationResult failure)) return failure;
            if (!ValidateContainmentRequest(newParentId, childId, request.kind, before, out failure)) return failure;

            LocationContainmentLinkData current = FindActiveParentLink(childId);
            if (current == null)
            {
                return Fail(LocationOperationStatus.MissingContainment, $"Location '{childId}' has no active parent to reparent.", before);
            }

            if (!string.IsNullOrWhiteSpace(oldParentId) && !string.Equals(current.parentLocationId, oldParentId, StringComparison.Ordinal))
            {
                return Fail(LocationOperationStatus.InvalidHierarchy, $"Location '{childId}' active parent is '{current.parentLocationId}', not '{oldParentId}'.", before);
            }

            if (string.Equals(current.parentLocationId, newParentId, StringComparison.Ordinal) && current.kind == request.kind)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location parent already matches requested parent.", before, before, duplicate: true);
            }

            if (WouldCreateCycle(newParentId, childId, current.linkId, out string cyclePath))
            {
                return Fail(LocationOperationStatus.CycleDetected, $"Reparent would create a cycle: {cyclePath}.", before);
            }

            if (GetDepth(newParentId, current.linkId) + 1 > MaxContainmentDepth)
            {
                return Fail(LocationOperationStatus.DepthLimitExceeded, $"Containment depth would exceed {MaxContainmentDepth}.", before);
            }

            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location reparent preview.", before, before, preview: true);
            }

            current.state = LocationLinkState.Ended;
            current.effectiveEndWorldTime = request.effectiveWorldTime;
            current.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? current.sourceEventId : Normalize(request.sourceEventId);
            current.sourceRecordId = string.IsNullOrWhiteSpace(request.sourceRecordId) ? current.sourceRecordId : Normalize(request.sourceRecordId);
            current.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? current.provenanceId : Normalize(request.provenanceId);
            current.revision++;
            LocationContainmentLinkData next = CreateContainmentLink(linkId, newParentId, childId, request);
            containmentLinksById.Add(next.linkId, next);
            RegisterTransaction(tx, "containment.reparent", next.linkId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(recordsById[childId]), "Location reparented.", before, Revision);
        }

        public LocationOperationResult EndContainment(LocationEndContainmentRequest request)
        {
            request ??= new LocationEndContainmentRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string linkId = Normalize(request.linkId);
            string childId = Normalize(request.childLocationId);
            if (string.IsNullOrWhiteSpace(linkId))
            {
                LocationContainmentLinkData active = FindActiveParentLink(childId);
                linkId = active?.linkId ?? string.Empty;
            }

            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, linkId, "containment.end", before, out LocationOperationResult duplicate)) return duplicate;
            if (!ValidateExpectedRevision(request.expectedRevision, before, out LocationOperationResult failure)) return failure;
            if (!containmentLinksById.TryGetValue(linkId, out LocationContainmentLinkData link))
            {
                return Fail(LocationOperationStatus.MissingContainment, $"Containment link '{linkId}' does not exist.", before);
            }

            if (!string.IsNullOrWhiteSpace(childId) && !string.Equals(link.childLocationId, childId, StringComparison.Ordinal))
            {
                return Fail(LocationOperationStatus.InvalidHierarchy, $"Containment link '{linkId}' does not target child '{childId}'.", before);
            }

            if (!IsActiveLink(link))
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[link.childLocationId]), "Location containment link is already inactive.", before, before, duplicate: true);
            }

            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[link.childLocationId]), "Location containment end preview.", before, before, preview: true);
            }

            link.state = LocationLinkState.Ended;
            link.effectiveEndWorldTime = request.effectiveWorldTime;
            link.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? link.sourceEventId : Normalize(request.sourceEventId);
            link.sourceRecordId = string.IsNullOrWhiteSpace(request.sourceRecordId) ? link.sourceRecordId : Normalize(request.sourceRecordId);
            link.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? link.provenanceId : Normalize(request.provenanceId);
            link.revision++;
            RegisterTransaction(tx, "containment.end", link.linkId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(recordsById[link.childLocationId]), "Location containment ended.", before, Revision);
        }

        public LocationOperationResult CreateSpatialRelationship(LocationSpatialRelationshipRequest request)
        {
            request ??= new LocationSpatialRelationshipRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string sourceId = Normalize(request.sourceLocationId);
            string targetId = Normalize(request.targetLocationId);
            string relationshipId = Normalize(request.relationshipId);
            if (string.IsNullOrWhiteSpace(relationshipId)) relationshipId = BuildSpatialRelationshipId(sourceId, targetId, request.kind);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, relationshipId, "spatial.create", before, out LocationOperationResult duplicate)) return duplicate;
            if (!ValidateExpectedRevision(request.expectedRevision, before, out LocationOperationResult failure)) return failure;
            if (!ValidateSpatialRequest(sourceId, targetId, request.kind, request.directionality, before, out failure)) return failure;

            if (spatialRelationshipsById.TryGetValue(relationshipId, out LocationSpatialRelationshipData existing))
            {
                if (existing.sourceLocationId == sourceId && existing.targetLocationId == targetId && existing.kind == request.kind)
                {
                    return LocationOperationResult.Success(BuildSnapshot(recordsById[sourceId]), "Spatial relationship already exists.", before, before, duplicate: true);
                }

                return Fail(LocationOperationStatus.InvalidRequest, $"Spatial relationship '{relationshipId}' already exists with different endpoints.", before);
            }

            LocationSpatialRelationshipData relationship = CreateSpatialRelationshipData(relationshipId, sourceId, targetId, request);
            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[sourceId]), "Spatial relationship preview.", before, before, preview: true);
            }

            spatialRelationshipsById.Add(relationship.relationshipId, relationship);
            RegisterTransaction(tx, "spatial.create", relationship.relationshipId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(recordsById[sourceId]), "Spatial relationship created.", before, Revision);
        }

        public LocationOperationResult EndSpatialRelationship(LocationEndSpatialRelationshipRequest request)
        {
            request ??= new LocationEndSpatialRelationshipRequest();
            long before = Revision;
            if (disposed) return Fail(LocationOperationStatus.Disposed, "Location runtime is disposed.", before);
            string relationshipId = Normalize(request.relationshipId);
            string tx = Normalize(request.transactionId);
            if (TryDuplicate(tx, relationshipId, "spatial.end", before, out LocationOperationResult duplicate)) return duplicate;
            if (!ValidateExpectedRevision(request.expectedRevision, before, out LocationOperationResult failure)) return failure;
            if (!spatialRelationshipsById.TryGetValue(relationshipId, out LocationSpatialRelationshipData relationship))
            {
                return Fail(LocationOperationStatus.MissingSpatialRelationship, $"Spatial relationship '{relationshipId}' does not exist.", before);
            }

            if (!IsActiveLink(relationship))
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[relationship.sourceLocationId]), "Spatial relationship is already inactive.", before, before, duplicate: true);
            }

            if (request.preview)
            {
                return LocationOperationResult.Success(BuildSnapshot(recordsById[relationship.sourceLocationId]), "Spatial relationship end preview.", before, before, preview: true);
            }

            relationship.state = LocationLinkState.Ended;
            relationship.effectiveEndWorldTime = request.effectiveWorldTime;
            relationship.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? relationship.sourceEventId : Normalize(request.sourceEventId);
            relationship.sourceRecordId = string.IsNullOrWhiteSpace(request.sourceRecordId) ? relationship.sourceRecordId : Normalize(request.sourceRecordId);
            relationship.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? relationship.provenanceId : Normalize(request.provenanceId);
            relationship.revision++;
            RegisterTransaction(tx, "spatial.end", relationship.relationshipId);
            Revision++;
            IsDirty = true;
            return LocationOperationResult.Success(BuildSnapshot(recordsById[relationship.sourceLocationId]), "Spatial relationship ended.", before, Revision);
        }

        public bool TryGetSnapshot(string locationId, out LocationSnapshot snapshot)
        {
            if (recordsById.TryGetValue(Normalize(locationId), out LocationRecordData record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetContainmentLink(string linkId, out LocationContainmentSnapshot snapshot)
        {
            if (containmentLinksById.TryGetValue(Normalize(linkId), out LocationContainmentLinkData link))
            {
                snapshot = BuildContainmentSnapshot(link);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetSpatialRelationship(string relationshipId, out LocationSpatialRelationshipSnapshot snapshot)
        {
            if (spatialRelationshipsById.TryGetValue(Normalize(relationshipId), out LocationSpatialRelationshipData relationship))
            {
                snapshot = BuildSpatialSnapshot(relationship);
                return true;
            }

            snapshot = null;
            return false;
        }

        public LocationContainmentSnapshot GetActiveParentLink(string childLocationId)
        {
            LocationContainmentLinkData link = FindActiveParentLink(Normalize(childLocationId));
            return link == null ? null : BuildContainmentSnapshot(link);
        }

        public IReadOnlyList<LocationContainmentSnapshot> GetChildLinks(string parentLocationId, bool includeHidden = false)
        {
            string id = Normalize(parentLocationId);
            return containmentLinksById.Values
                .Where(link => IsActiveLink(link) && string.Equals(link.parentLocationId, id, StringComparison.Ordinal) && (includeHidden || IsVisibleForNormalProjection(link.visibility)))
                .OrderBy(link => link.childLocationId, StringComparer.Ordinal)
                .ThenBy(link => link.linkId, StringComparer.Ordinal)
                .Select(BuildContainmentSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationSnapshot> GetChildren(string parentLocationId, bool includeHidden = false)
        {
            return GetChildLinks(parentLocationId, includeHidden)
                .Select(link => recordsById.TryGetValue(link.ChildLocationId, out LocationRecordData record) ? BuildSnapshot(record) : null)
                .Where(snapshot => snapshot != null)
                .OrderBy(snapshot => snapshot.LocationId, StringComparer.Ordinal)
                .ToArray();
        }

        public LocationHierarchyPathResult GetHierarchyPath(string locationId)
        {
            string id = Normalize(locationId);
            List<LocationSnapshot> path = new List<LocationSnapshot>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            bool truncated = false;
            while (!string.IsNullOrWhiteSpace(id) && recordsById.TryGetValue(id, out LocationRecordData record))
            {
                if (!visited.Add(id) || path.Count > MaxContainmentDepth)
                {
                    truncated = true;
                    break;
                }

                path.Add(BuildSnapshot(record));
                id = FindActiveParentLink(id)?.parentLocationId;
            }

            path.Reverse();
            return new LocationHierarchyPathResult(path, truncated, truncated ? "Hierarchy path exceeded depth or encountered a cycle." : "Hierarchy path resolved.");
        }

        public IReadOnlyList<LocationSnapshot> GetAncestors(string locationId)
        {
            LocationHierarchyPathResult path = GetHierarchyPath(locationId);
            return path.Path.Take(Math.Max(0, path.Path.Count - 1)).ToArray();
        }

        public IReadOnlyList<LocationSnapshot> GetDescendants(string locationId, bool includeHidden = false)
        {
            string rootId = Normalize(locationId);
            List<LocationSnapshot> descendants = new List<LocationSnapshot>();
            Queue<string> queue = new Queue<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(rootId);
            visited.Add(rootId);

            while (queue.Count > 0)
            {
                string parent = queue.Dequeue();
                foreach (LocationContainmentLinkData link in containmentLinksById.Values
                    .Where(value => IsActiveLink(value) && string.Equals(value.parentLocationId, parent, StringComparison.Ordinal) && (includeHidden || IsVisibleForNormalProjection(value.visibility)))
                    .OrderBy(value => value.childLocationId, StringComparer.Ordinal)
                    .ThenBy(value => value.linkId, StringComparer.Ordinal))
                {
                    if (!visited.Add(link.childLocationId))
                    {
                        continue;
                    }

                    if (recordsById.TryGetValue(link.childLocationId, out LocationRecordData child))
                    {
                        descendants.Add(BuildSnapshot(child));
                        queue.Enqueue(child.locationId);
                    }
                }
            }

            return descendants.OrderBy(snapshot => snapshot.LocationId, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<LocationSnapshot> GetRoots(bool includeHidden = false)
        {
            HashSet<string> children = new HashSet<string>(containmentLinksById.Values.Where(IsActiveLink).Select(link => link.childLocationId), StringComparer.Ordinal);
            return recordsById.Values
                .Where(record => !children.Contains(record.locationId) && (includeHidden || IsVisibleForNormalProjection(record.visibility)))
                .OrderBy(record => record.locationId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationSpatialRelationshipSnapshot> GetSpatialRelationships(string locationId, bool includeIncoming = true, bool includeHidden = false)
        {
            string id = Normalize(locationId);
            return spatialRelationshipsById.Values
                .Where(relationship => IsActiveLink(relationship)
                    && (string.Equals(relationship.sourceLocationId, id, StringComparison.Ordinal)
                        || includeIncoming && string.Equals(relationship.targetLocationId, id, StringComparison.Ordinal)
                        || relationship.directionality == LocationSpatialDirectionality.Symmetric && string.Equals(relationship.targetLocationId, id, StringComparison.Ordinal))
                    && (includeHidden || IsVisibleForNormalProjection(relationship.visibility)))
                .OrderBy(relationship => relationship.relationshipId, StringComparer.Ordinal)
                .Select(BuildSpatialSnapshot)
                .ToArray();
        }

        public bool AreSpatiallyRelated(string sourceLocationId, string targetLocationId, LocationSpatialRelationshipKind kind)
        {
            string source = Normalize(sourceLocationId);
            string target = Normalize(targetLocationId);
            return spatialRelationshipsById.Values.Any(relationship => IsActiveLink(relationship) && RelationshipMatches(relationship, source, target, kind));
        }

        public LocationReferenceResolutionResult ResolveReference(LocationReferenceData reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.locationId))
            {
                return LocationReferenceResolutionResult.Failure(LocationReferenceResolutionStatus.InvalidRequest, "Location reference is empty.");
            }

            if (!string.IsNullOrWhiteSpace(reference.worldId) && !string.Equals(Normalize(reference.worldId), worldId, StringComparison.Ordinal))
            {
                return LocationReferenceResolutionResult.Failure(LocationReferenceResolutionStatus.WrongWorld, $"Location reference targets world '{reference.worldId}', not '{worldId}'.");
            }

            if (!recordsById.TryGetValue(Normalize(reference.locationId), out LocationRecordData record))
            {
                return LocationReferenceResolutionResult.Failure(LocationReferenceResolutionStatus.MissingLocation, $"Location '{reference.locationId}' does not exist.");
            }

            if (record.lifecycleState == LocationLifecycleState.Destroyed || record.lifecycleState == LocationLifecycleState.Removed)
            {
                return LocationReferenceResolutionResult.Failure(LocationReferenceResolutionStatus.Destroyed, $"Location '{reference.locationId}' is no longer active.");
            }

            return LocationReferenceResolutionResult.Resolved(BuildSnapshot(record));
        }

        public IReadOnlyList<LocationSnapshot> QueryByCategory(LocationCategory category)
        {
            return recordsById.Values
                .Where(record => TryGetDefinition(record.locationDefinitionId, out LocationDefinition definition, 0L, out _) && definition.Category == category)
                .OrderBy(record => record.locationId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToArray();
        }

        public IReadOnlyList<LocationSnapshot> QueryByDefinition(string definitionId)
        {
            string id = Normalize(definitionId);
            return recordsById.Values.Where(record => string.Equals(record.locationDefinitionId, id, StringComparison.Ordinal)).OrderBy(record => record.locationId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        }

        public IReadOnlyList<LocationSnapshot> QueryByTag(string tagId)
        {
            string id = Normalize(tagId);
            return recordsById.Values.Where(record => (record.semanticTagIds ?? Array.Empty<string>()).Contains(id, StringComparer.Ordinal)).OrderBy(record => record.locationId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        }

        public IReadOnlyList<LocationSnapshot> QueryByLifecycle(LocationLifecycleState state)
        {
            return recordsById.Values.Where(record => record.lifecycleState == state).OrderBy(record => record.locationId, StringComparer.Ordinal).Select(BuildSnapshot).ToArray();
        }

        public LocationRuntimeSaveData CreateSaveData()
        {
            return new LocationRuntimeSaveData
            {
                schemaVersion = LocationRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                records = recordsById.Values.OrderBy(record => record.locationId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                names = namesById.Values.OrderBy(record => record.nameRecordId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(record => record.transactionId, StringComparer.Ordinal).Select(record => record.Clone()).ToList(),
                containmentLinks = containmentLinksById.Values.OrderBy(link => link.linkId, StringComparer.Ordinal).Select(link => link.Clone()).ToList(),
                spatialRelationships = spatialRelationshipsById.Values.OrderBy(relationship => relationship.relationshipId, StringComparer.Ordinal).Select(relationship => relationship.Clone()).ToList()
            };
        }

        public LocationOperationResult RestoreFromSaveData(LocationRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, string expectedWorldId = PersistenceService.LocalWorldId, bool restoring = true)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, expectedWorldId, knownPropertyIds, knownOrganizationIds, knownGovernmentIds, knownTerritoryIds, out string failure))
            {
                return Fail(LocationOperationStatus.PersistenceInvalid, failure, before);
            }

            LocationRuntimeSaveData rollback = CreateSaveData();
            try
            {
                RestoreInternal(saveData ?? new LocationRuntimeSaveData());
                registry = definitionRegistry ?? registry;
                worldId = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
                IsDirty = !restoring;
                return LocationOperationResult.Success(null, "Locations restored.", before, Revision);
            }
            catch (Exception exception)
            {
                RestoreInternal(rollback);
                return Fail(LocationOperationStatus.RestoreFailed, $"Location restore failed: {exception.Message}", before);
            }
        }

        public LocationValidationReport ValidateRuntime()
        {
            ValidateSaveData(CreateSaveData(), registry, worldId, knownPropertyIds, knownOrganizationIds, knownGovernmentIds, knownTerritoryIds, out _, out LocationValidationReport report);
            return report;
        }

        public static bool ValidateSaveData(
            LocationRuntimeSaveData saveData,
            DefinitionRegistry registry,
            string expectedWorldId,
            IEnumerable<string> knownProperties,
            IEnumerable<string> knownOrganizations,
            IEnumerable<string> knownGovernments,
            IEnumerable<string> knownTerritories,
            out string failure)
        {
            return ValidateSaveData(saveData, registry, expectedWorldId, knownProperties, knownOrganizations, knownGovernments, knownTerritories, out failure, out _);
        }

        public static bool ValidateSaveData(
            LocationRuntimeSaveData saveData,
            DefinitionRegistry registry,
            string expectedWorldId,
            IEnumerable<string> knownProperties,
            IEnumerable<string> knownOrganizations,
            IEnumerable<string> knownGovernments,
            IEnumerable<string> knownTerritories,
            out string failure,
            out LocationValidationReport report)
        {
            report = new LocationValidationReport();
            saveData ??= new LocationRuntimeSaveData();
            string world = string.IsNullOrWhiteSpace(expectedWorldId) ? PersistenceService.LocalWorldId : expectedWorldId.Trim();
            if (saveData.schemaVersion != LocationRuntimeSaveData.CurrentSchemaVersion) report.AddError($"Unsupported location save schema {saveData.schemaVersion}; expected {LocationRuntimeSaveData.CurrentSchemaVersion}.");
            if (!string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), world, StringComparison.Ordinal)) report.AddError($"Location save world '{saveData.worldId}' does not match expected world '{world}'.");

            HashSet<string> recordIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> nameIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> containmentIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> spatialIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> properties = new HashSet<string>(Clean(knownProperties), StringComparer.Ordinal);
            HashSet<string> organizations = new HashSet<string>(Clean(knownOrganizations), StringComparer.Ordinal);
            HashSet<string> governments = new HashSet<string>(Clean(knownGovernments), StringComparer.Ordinal);
            HashSet<string> territories = new HashSet<string>(Clean(knownTerritories), StringComparer.Ordinal);
            Dictionary<string, LocationRecordData> recordMap = new Dictionary<string, LocationRecordData>(StringComparer.Ordinal);
            Dictionary<string, LocationDefinition> definitionMap = new Dictionary<string, LocationDefinition>(StringComparer.Ordinal);

            foreach (LocationRecordData record in saveData.records ?? new List<LocationRecordData>())
            {
                if (record == null)
                {
                    report.AddError("Location save contains a null record.");
                    continue;
                }

                string locationId = Normalize(record.locationId);
                if (string.IsNullOrWhiteSpace(locationId)) report.AddError("Location record is missing a stable location ID.");
                else if (!recordIds.Add(locationId)) report.AddError($"Duplicate location ID '{locationId}'.");
                else recordMap[locationId] = record;
                if (string.IsNullOrWhiteSpace(record.locationDefinitionId)) report.AddError($"Location '{locationId}' is missing a definition ID.");
                else if (registry == null || !registry.TryGet(Normalize(record.locationDefinitionId), out LocationDefinition definition))
                {
                    report.AddError($"Location '{locationId}' references missing Location Definition '{record.locationDefinitionId}'.");
                    definition = null;
                }
                else
                {
                    definitionMap[locationId] = definition;
                    if (!definition.SupportsVisibility(record.visibility)) report.AddError($"Location '{locationId}' visibility '{record.visibility}' is not supported by definition '{definition.Id}'.");
                    foreach (string tag in Clean(record.semanticTagIds))
                    {
                        if (!definition.AllowsSemanticTag(tag)) report.AddError($"Location '{locationId}' tag '{tag}' is not supported by definition '{definition.Id}'.");
                    }

                    if (!string.IsNullOrWhiteSpace(record.associatedPropertyId) && !definition.SupportsPropertyAssociation) report.AddError($"Location '{locationId}' cannot reference a property.");
                    if (!string.IsNullOrWhiteSpace(record.associatedOrganizationId) && !definition.SupportsOrganizationAssociation) report.AddError($"Location '{locationId}' cannot reference an organization.");
                    if (!string.IsNullOrWhiteSpace(record.associatedGovernmentId) && !definition.SupportsGovernmentAssociation) report.AddError($"Location '{locationId}' cannot reference a government.");
                    if ((record.associatedTerritoryIds ?? Array.Empty<string>()).Length > 0 && !definition.SupportsTerritoryAssociation) report.AddError($"Location '{locationId}' cannot reference territories.");
                }

                if (!string.IsNullOrWhiteSpace(record.authoredPlaceDefinitionId)
                    && RegistryContainsAuthoredPlaces(registry)
                    && !registry.TryGet(Normalize(record.authoredPlaceDefinitionId), out PlaceDefinition _))
                {
                    report.AddError($"Location '{locationId}' references missing authored Place Definition '{record.authoredPlaceDefinitionId}'.");
                }

                if (!string.IsNullOrWhiteSpace(record.worldId) && !string.Equals(record.worldId.Trim(), world, StringComparison.Ordinal)) report.AddError($"Location '{locationId}' belongs to world '{record.worldId}', not '{world}'.");
                if (string.IsNullOrWhiteSpace(record.officialName)) report.AddError($"Location '{locationId}' has no official name.");
                if (!Enum.IsDefined(typeof(LocationLifecycleState), record.lifecycleState) || record.lifecycleState == LocationLifecycleState.Unknown) report.AddError($"Location '{locationId}' has invalid lifecycle '{record.lifecycleState}'.");
                if (properties.Count > 0 && !string.IsNullOrWhiteSpace(record.associatedPropertyId) && !properties.Contains(record.associatedPropertyId)) report.AddError($"Location '{locationId}' references unknown property '{record.associatedPropertyId}'.");
                if (organizations.Count > 0 && !string.IsNullOrWhiteSpace(record.associatedOrganizationId) && !organizations.Contains(record.associatedOrganizationId)) report.AddError($"Location '{locationId}' references unknown organization '{record.associatedOrganizationId}'.");
                if (governments.Count > 0 && !string.IsNullOrWhiteSpace(record.associatedGovernmentId) && !governments.Contains(record.associatedGovernmentId)) report.AddError($"Location '{locationId}' references unknown government '{record.associatedGovernmentId}'.");
                foreach (string territory in Clean(record.associatedTerritoryIds))
                {
                    if (territories.Count > 0 && !territories.Contains(territory)) report.AddError($"Location '{locationId}' references unknown territory '{territory}'.");
                }
            }

            Dictionary<string, string> activeParentByChild = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LocationContainmentLinkData link in saveData.containmentLinks ?? new List<LocationContainmentLinkData>())
            {
                if (link == null)
                {
                    report.AddError("Location save contains a null containment link.");
                    continue;
                }

                string linkId = Normalize(link.linkId);
                string parentId = Normalize(link.parentLocationId);
                string childId = Normalize(link.childLocationId);
                if (string.IsNullOrWhiteSpace(linkId)) report.AddError("Location containment link has no ID.");
                else if (!containmentIds.Add(linkId)) report.AddError($"Duplicate location containment link ID '{linkId}'.");
                if (!recordIds.Contains(parentId)) report.AddError($"Location containment link '{linkId}' references missing parent location '{link.parentLocationId}'.");
                if (!recordIds.Contains(childId)) report.AddError($"Location containment link '{linkId}' references missing child location '{link.childLocationId}'.");
                if (parentId == childId) report.AddError($"Location containment link '{linkId}' cannot parent a location to itself.");
                if (!Enum.IsDefined(typeof(LocationContainmentKind), link.kind) || link.kind == LocationContainmentKind.Unknown) report.AddError($"Location containment link '{linkId}' has invalid kind '{link.kind}'.");
                if (!Enum.IsDefined(typeof(LocationLinkState), link.state) || link.state == LocationLinkState.Unknown) report.AddError($"Location containment link '{linkId}' has invalid state '{link.state}'.");
                if (IsActiveState(link.state))
                {
                    if (activeParentByChild.ContainsKey(childId)) report.AddError($"Location '{childId}' has more than one active primary parent.");
                    else activeParentByChild[childId] = parentId;
                }

                if (definitionMap.TryGetValue(parentId, out LocationDefinition parentDefinition) && definitionMap.TryGetValue(childId, out LocationDefinition childDefinition))
                {
                    if (!parentDefinition.FutureContainmentAllowed || !childDefinition.FutureContainmentAllowed) report.AddError($"Location containment link '{linkId}' uses a definition that does not allow containment.");
                    if (!CanContain(parentDefinition.Category, childDefinition.Category)) report.AddError($"Location containment link '{linkId}' has invalid hierarchy {parentDefinition.Category}->{childDefinition.Category}.");
                }
            }

            foreach (KeyValuePair<string, string> edge in activeParentByChild)
            {
                if (CreatesCycle(edge.Key, activeParentByChild, out string cyclePath)) report.AddError($"Location containment graph contains a cycle: {cyclePath}.");
                if (StaticDepth(edge.Key, activeParentByChild) > MaxContainmentDepth) report.AddError($"Location containment graph exceeds max depth {MaxContainmentDepth} at '{edge.Key}'.");
            }

            foreach (LocationSpatialRelationshipData relationship in saveData.spatialRelationships ?? new List<LocationSpatialRelationshipData>())
            {
                if (relationship == null)
                {
                    report.AddError("Location save contains a null spatial relationship.");
                    continue;
                }

                string relationshipId = Normalize(relationship.relationshipId);
                string sourceId = Normalize(relationship.sourceLocationId);
                string targetId = Normalize(relationship.targetLocationId);
                if (string.IsNullOrWhiteSpace(relationshipId)) report.AddError("Location spatial relationship has no ID.");
                else if (!spatialIds.Add(relationshipId)) report.AddError($"Duplicate location spatial relationship ID '{relationshipId}'.");
                if (!recordIds.Contains(sourceId)) report.AddError($"Location spatial relationship '{relationshipId}' references missing source location '{relationship.sourceLocationId}'.");
                if (!recordIds.Contains(targetId)) report.AddError($"Location spatial relationship '{relationshipId}' references missing target location '{relationship.targetLocationId}'.");
                if (sourceId == targetId) report.AddError($"Location spatial relationship '{relationshipId}' cannot relate a location to itself.");
                if (!Enum.IsDefined(typeof(LocationSpatialRelationshipKind), relationship.kind) || relationship.kind == LocationSpatialRelationshipKind.Unknown) report.AddError($"Location spatial relationship '{relationshipId}' has invalid kind '{relationship.kind}'.");
                if (!Enum.IsDefined(typeof(LocationSpatialDirectionality), relationship.directionality) || relationship.directionality == LocationSpatialDirectionality.Unknown) report.AddError($"Location spatial relationship '{relationshipId}' has invalid directionality '{relationship.directionality}'.");
                if (!Enum.IsDefined(typeof(LocationLinkState), relationship.state) || relationship.state == LocationLinkState.Unknown) report.AddError($"Location spatial relationship '{relationshipId}' has invalid state '{relationship.state}'.");
            }

            foreach (LocationNameRecordData name in saveData.names ?? new List<LocationNameRecordData>())
            {
                if (name == null)
                {
                    report.AddError("Location save contains a null name record.");
                    continue;
                }

                string nameId = Normalize(name.nameRecordId);
                if (string.IsNullOrWhiteSpace(nameId)) report.AddError("Location name record is missing a stable ID.");
                else if (!nameIds.Add(nameId)) report.AddError($"Duplicate location name record ID '{nameId}'.");
                if (!recordIds.Contains(Normalize(name.locationId))) report.AddError($"Location name '{nameId}' references missing location '{name.locationId}'.");
                if (string.IsNullOrWhiteSpace(name.value)) report.AddError($"Location name '{nameId}' has no value.");
            }

            foreach (LocationRecordData record in saveData.records ?? new List<LocationRecordData>())
            {
                if (record != null && !string.IsNullOrWhiteSpace(record.currentOfficialNameRecordId) && !nameIds.Contains(Normalize(record.currentOfficialNameRecordId)))
                {
                    report.AddError($"Location '{record.locationId}' references missing current official name '{record.currentOfficialNameRecordId}'.");
                }
            }

            foreach (LocationTransactionRecordData tx in saveData.transactions ?? new List<LocationTransactionRecordData>())
            {
                if (tx == null) continue;
                if (string.IsNullOrWhiteSpace(tx.transactionId)) report.AddError("Location transaction has no ID.");
                string entityId = Normalize(tx.locationId);
                if (!string.IsNullOrWhiteSpace(entityId) && !recordIds.Contains(entityId) && !containmentIds.Contains(entityId) && !spatialIds.Contains(entityId)) report.AddError($"Location transaction '{tx.transactionId}' references missing location entity '{tx.locationId}'.");
            }

            failure = report.Summary;
            return report.Succeeded;
        }

        public void Reset()
        {
            recordsById.Clear();
            namesById.Clear();
            transactionsById.Clear();
            containmentLinksById.Clear();
            spatialRelationshipsById.Clear();
            Revision = 0L;
            IsDirty = false;
            disposed = false;
        }

        public void Dispose()
        {
            Reset();
            disposed = true;
        }

        private LocationRecordData CreateRecord(LocationCreateRequest request, LocationDefinition definition, string locationId, string officialName, string[] tags)
        {
            return new LocationRecordData
            {
                locationId = locationId,
                locationDefinitionId = Normalize(definition.Id),
                authoredPlaceDefinitionId = Normalize(request.authoredPlaceDefinitionId),
                worldId = worldId,
                currentOfficialNameRecordId = BuildNameId(locationId, "official", 1),
                officialName = officialName,
                commonName = NormalizeName(request.commonName),
                aliases = Clean(request.aliases),
                lifecycleState = request.initialLifecycleState == LocationLifecycleState.Unknown ? LocationLifecycleState.Active : request.initialLifecycleState,
                createdWorldTime = request.createdWorldTime,
                semanticTagIds = tags,
                associatedPropertyId = Normalize(request.associatedPropertyId),
                associatedOrganizationId = Normalize(request.associatedOrganizationId),
                associatedGovernmentId = Normalize(request.associatedGovernmentId),
                associatedTerritoryIds = Clean(request.associatedTerritoryIds),
                associations = (request.associations ?? Array.Empty<LocationAssociationReferenceData>()).Where(value => value != null).Select(value => value.Clone()).OrderBy(value => value.kind.ToString(), StringComparer.Ordinal).ThenBy(value => value.referenceId, StringComparer.Ordinal).ToArray(),
                prototypeSceneBindingKey = definition.AllowsSceneBinding ? Normalize(request.prototypeSceneBindingKey) : string.Empty,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
        }

        private static bool RegistryContainsAuthoredPlaces(DefinitionRegistry definitionRegistry)
        {
            return definitionRegistry != null && definitionRegistry.DefinitionsById.Values.Any(value => value is PlaceDefinition);
        }

        private void RestoreInternal(LocationRuntimeSaveData saveData)
        {
            recordsById.Clear();
            namesById.Clear();
            transactionsById.Clear();
            containmentLinksById.Clear();
            spatialRelationshipsById.Clear();
            foreach (LocationRecordData record in saveData.records ?? new List<LocationRecordData>()) recordsById[Normalize(record.locationId)] = record.Clone();
            foreach (LocationNameRecordData name in saveData.names ?? new List<LocationNameRecordData>()) namesById[Normalize(name.nameRecordId)] = name.Clone();
            foreach (LocationTransactionRecordData tx in saveData.transactions ?? new List<LocationTransactionRecordData>()) transactionsById[Normalize(tx.transactionId)] = tx.Clone();
            foreach (LocationContainmentLinkData link in saveData.containmentLinks ?? new List<LocationContainmentLinkData>()) containmentLinksById[Normalize(link.linkId)] = link.Clone();
            foreach (LocationSpatialRelationshipData relationship in saveData.spatialRelationships ?? new List<LocationSpatialRelationshipData>()) spatialRelationshipsById[Normalize(relationship.relationshipId)] = relationship.Clone();
            Revision = Math.Max(0L, saveData.revision);
        }

        private bool ValidateExpectedRevision(long expectedRevision, long before, out LocationOperationResult failure)
        {
            failure = null;
            if (expectedRevision >= 0L && expectedRevision != Revision)
            {
                failure = Fail(LocationOperationStatus.RevisionConflict, $"Expected location runtime revision {expectedRevision}, but current revision is {Revision}.", before);
                return false;
            }

            return true;
        }

        private bool ValidateContainmentRequest(string parentId, string childId, LocationContainmentKind kind, long before, out LocationOperationResult failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
            {
                return SetFailure(LocationOperationStatus.InvalidRequest, "Containment parent and child location IDs are required.", before, out failure);
            }

            if (parentId == childId)
            {
                return SetFailure(LocationOperationStatus.InvalidHierarchy, "A location cannot contain itself.", before, out failure);
            }

            if (!recordsById.TryGetValue(parentId, out LocationRecordData parent))
            {
                return SetFailure(LocationOperationStatus.MissingLocation, $"Parent location '{parentId}' does not exist.", before, out failure);
            }

            if (!recordsById.TryGetValue(childId, out LocationRecordData child))
            {
                return SetFailure(LocationOperationStatus.MissingLocation, $"Child location '{childId}' does not exist.", before, out failure);
            }

            if (!string.Equals(parent.worldId, worldId, StringComparison.Ordinal) || !string.Equals(child.worldId, worldId, StringComparison.Ordinal))
            {
                return SetFailure(LocationOperationStatus.WrongWorld, "Containment endpoints must belong to this runtime world.", before, out failure);
            }

            if (!Enum.IsDefined(typeof(LocationContainmentKind), kind) || kind == LocationContainmentKind.Unknown)
            {
                return SetFailure(LocationOperationStatus.InvalidHierarchy, $"Containment kind '{kind}' is invalid.", before, out failure);
            }

            if (!TryGetDefinition(parent.locationDefinitionId, out LocationDefinition parentDefinition, before, out failure)) return false;
            if (!TryGetDefinition(child.locationDefinitionId, out LocationDefinition childDefinition, before, out failure)) return false;
            if (!parentDefinition.FutureContainmentAllowed || !childDefinition.FutureContainmentAllowed)
            {
                return SetFailure(LocationOperationStatus.UnsupportedByDefinition, "One or more location definitions do not allow containment.", before, out failure);
            }

            if (!CanContain(parentDefinition.Category, childDefinition.Category))
            {
                return SetFailure(LocationOperationStatus.InvalidHierarchy, $"Location hierarchy {parentDefinition.Category}->{childDefinition.Category} is not supported.", before, out failure);
            }

            return true;
        }

        private bool ValidateSpatialRequest(string sourceId, string targetId, LocationSpatialRelationshipKind kind, LocationSpatialDirectionality directionality, long before, out LocationOperationResult failure)
        {
            failure = null;
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                return SetFailure(LocationOperationStatus.InvalidRequest, "Spatial relationship source and target location IDs are required.", before, out failure);
            }

            if (sourceId == targetId)
            {
                return SetFailure(LocationOperationStatus.InvalidReference, "A spatial relationship cannot target the same location.", before, out failure);
            }

            if (!recordsById.ContainsKey(sourceId))
            {
                return SetFailure(LocationOperationStatus.MissingLocation, $"Source location '{sourceId}' does not exist.", before, out failure);
            }

            if (!recordsById.ContainsKey(targetId))
            {
                return SetFailure(LocationOperationStatus.MissingLocation, $"Target location '{targetId}' does not exist.", before, out failure);
            }

            if (!Enum.IsDefined(typeof(LocationSpatialRelationshipKind), kind) || kind == LocationSpatialRelationshipKind.Unknown)
            {
                return SetFailure(LocationOperationStatus.InvalidReference, $"Spatial relationship kind '{kind}' is invalid.", before, out failure);
            }

            if (!Enum.IsDefined(typeof(LocationSpatialDirectionality), directionality) || directionality == LocationSpatialDirectionality.Unknown)
            {
                return SetFailure(LocationOperationStatus.InvalidReference, $"Spatial relationship directionality '{directionality}' is invalid.", before, out failure);
            }

            return true;
        }

        private LocationContainmentLinkData CreateContainmentLink(string linkId, string parentId, string childId, LocationContainmentRequest request)
        {
            return new LocationContainmentLinkData
            {
                linkId = linkId,
                parentLocationId = parentId,
                childLocationId = childId,
                kind = request.kind == LocationContainmentKind.Unknown ? LocationContainmentKind.Primary : request.kind,
                state = LocationLinkState.Active,
                effectiveStartWorldTime = request.effectiveWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
        }

        private LocationContainmentLinkData CreateContainmentLink(string linkId, string parentId, string childId, LocationReparentRequest request)
        {
            return new LocationContainmentLinkData
            {
                linkId = linkId,
                parentLocationId = parentId,
                childLocationId = childId,
                kind = request.kind == LocationContainmentKind.Unknown ? LocationContainmentKind.Primary : request.kind,
                state = LocationLinkState.Active,
                effectiveStartWorldTime = request.effectiveWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
        }

        private LocationSpatialRelationshipData CreateSpatialRelationshipData(string relationshipId, string sourceId, string targetId, LocationSpatialRelationshipRequest request)
        {
            return new LocationSpatialRelationshipData
            {
                relationshipId = relationshipId,
                sourceLocationId = sourceId,
                targetLocationId = targetId,
                kind = request.kind,
                directionality = request.directionality == LocationSpatialDirectionality.Unknown ? LocationSpatialDirectionality.Directional : request.directionality,
                inverseKind = request.inverseKind == LocationSpatialRelationshipKind.Unknown ? InverseOf(request.kind) : request.inverseKind,
                state = LocationLinkState.Active,
                effectiveStartWorldTime = request.effectiveWorldTime,
                visibility = request.visibility,
                sourceEventId = Normalize(request.sourceEventId),
                sourceRecordId = Normalize(request.sourceRecordId),
                provenanceId = Normalize(request.provenanceId),
                revision = 1L
            };
        }

        private LocationContainmentLinkData FindActiveParentLink(string childId, string excludedLinkId = null)
        {
            string child = Normalize(childId);
            string excluded = Normalize(excludedLinkId);
            return containmentLinksById.Values
                .Where(link => IsActiveLink(link) && string.Equals(link.childLocationId, child, StringComparison.Ordinal) && !string.Equals(link.linkId, excluded, StringComparison.Ordinal))
                .OrderBy(link => link.effectiveStartWorldTime)
                .ThenBy(link => link.linkId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private bool WouldCreateCycle(string parentId, string childId, string excludedLinkId, out string cyclePath)
        {
            string current = Normalize(parentId);
            string child = Normalize(childId);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            List<string> path = new List<string> { child, current };
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (current == child)
                {
                    cyclePath = string.Join(" -> ", path);
                    return true;
                }

                if (!visited.Add(current))
                {
                    cyclePath = string.Join(" -> ", path.Concat(new[] { current }));
                    return true;
                }

                LocationContainmentLinkData parent = FindActiveParentLink(current, excludedLinkId);
                current = parent?.parentLocationId;
                if (!string.IsNullOrWhiteSpace(current)) path.Add(current);
            }

            cyclePath = string.Empty;
            return false;
        }

        private int GetDepth(string locationId, string excludedLinkId)
        {
            int depth = 0;
            string current = Normalize(locationId);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                LocationContainmentLinkData parent = FindActiveParentLink(current, excludedLinkId);
                if (parent == null) break;
                depth++;
                current = parent.parentLocationId;
            }

            return depth;
        }

        private static bool RelationshipMatches(LocationSpatialRelationshipData relationship, string source, string target, LocationSpatialRelationshipKind kind)
        {
            if (relationship.sourceLocationId == source && relationship.targetLocationId == target && relationship.kind == kind) return true;
            if (relationship.directionality == LocationSpatialDirectionality.Symmetric && relationship.sourceLocationId == target && relationship.targetLocationId == source && relationship.kind == kind) return true;
            return relationship.targetLocationId == source && relationship.sourceLocationId == target && relationship.inverseKind == kind;
        }

        private static bool IsActiveLink(LocationContainmentLinkData link) => link != null && IsActiveState(link.state);
        private static bool IsActiveLink(LocationSpatialRelationshipData relationship) => relationship != null && IsActiveState(relationship.state);
        private static bool IsActiveState(LocationLinkState state) => state == LocationLinkState.Active;
        private static bool IsVisibleForNormalProjection(LocationVisibility visibility) => visibility == LocationVisibility.Public || visibility == LocationVisibility.Restricted;

        private static bool CanContain(LocationCategory parent, LocationCategory child)
        {
            if (parent == LocationCategory.Custom || child == LocationCategory.Custom) return true;
            if (child == LocationCategory.World) return false;
            return parent switch
            {
                LocationCategory.World => child == LocationCategory.Region || child == LocationCategory.Settlement || child == LocationCategory.Wilderness || child == LocationCategory.Dungeon,
                LocationCategory.Region => child == LocationCategory.Settlement || child == LocationCategory.District || child == LocationCategory.Wilderness || child == LocationCategory.Dungeon || child == LocationCategory.RouteAnchor,
                LocationCategory.Settlement => child == LocationCategory.District || child == LocationCategory.Building || child == LocationCategory.FunctionalArea || child == LocationCategory.Wilderness || child == LocationCategory.Dungeon || child == LocationCategory.RouteAnchor,
                LocationCategory.District => child == LocationCategory.Building || child == LocationCategory.FunctionalArea || child == LocationCategory.InteractionPoint || child == LocationCategory.RouteAnchor,
                LocationCategory.Building => child == LocationCategory.Room || child == LocationCategory.FunctionalArea || child == LocationCategory.InteractionPoint || child == LocationCategory.Dungeon,
                LocationCategory.Room => child == LocationCategory.Room || child == LocationCategory.FunctionalArea || child == LocationCategory.InteractionPoint,
                LocationCategory.FunctionalArea => child == LocationCategory.InteractionPoint,
                LocationCategory.Dungeon => child == LocationCategory.Room || child == LocationCategory.FunctionalArea || child == LocationCategory.InteractionPoint,
                LocationCategory.Wilderness => child == LocationCategory.FunctionalArea || child == LocationCategory.InteractionPoint || child == LocationCategory.RouteAnchor || child == LocationCategory.Dungeon,
                _ => false
            };
        }

        private static bool CreatesCycle(string childId, IReadOnlyDictionary<string, string> activeParentByChild, out string cyclePath)
        {
            string current = childId;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            List<string> path = new List<string>();
            while (!string.IsNullOrWhiteSpace(current) && activeParentByChild.TryGetValue(current, out string parent))
            {
                path.Add(current);
                if (!visited.Add(current))
                {
                    cyclePath = string.Join(" -> ", path);
                    return true;
                }

                current = parent;
            }

            cyclePath = string.Empty;
            return false;
        }

        private static int StaticDepth(string childId, IReadOnlyDictionary<string, string> activeParentByChild)
        {
            int depth = 0;
            string current = childId;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current) && activeParentByChild.TryGetValue(current, out string parent))
            {
                depth++;
                current = parent;
            }

            return depth;
        }

        private static LocationSpatialRelationshipKind InverseOf(LocationSpatialRelationshipKind kind)
        {
            return kind switch
            {
                LocationSpatialRelationshipKind.Above => LocationSpatialRelationshipKind.Below,
                LocationSpatialRelationshipKind.Below => LocationSpatialRelationshipKind.Above,
                LocationSpatialRelationshipKind.NorthOf => LocationSpatialRelationshipKind.SouthOf,
                LocationSpatialRelationshipKind.SouthOf => LocationSpatialRelationshipKind.NorthOf,
                LocationSpatialRelationshipKind.EastOf => LocationSpatialRelationshipKind.WestOf,
                LocationSpatialRelationshipKind.WestOf => LocationSpatialRelationshipKind.EastOf,
                LocationSpatialRelationshipKind.Adjacent => LocationSpatialRelationshipKind.Adjacent,
                LocationSpatialRelationshipKind.Near => LocationSpatialRelationshipKind.Near,
                LocationSpatialRelationshipKind.Overlaps => LocationSpatialRelationshipKind.Overlaps,
                LocationSpatialRelationshipKind.AcrossFrom => LocationSpatialRelationshipKind.AcrossFrom,
                LocationSpatialRelationshipKind.SharesBoundary => LocationSpatialRelationshipKind.SharesBoundary,
                _ => LocationSpatialRelationshipKind.Unknown
            };
        }

        private bool ValidateAssociations(LocationDefinition definition, string propertyId, string organizationId, string governmentId, IEnumerable<string> territoryIds, IEnumerable<LocationAssociationReferenceData> associations, long before, out LocationOperationResult failure)
        {
            failure = null;
            if (!string.IsNullOrWhiteSpace(propertyId) && !definition.SupportsPropertyAssociation) return SetFailure(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not support property association.", before, out failure);
            if (!string.IsNullOrWhiteSpace(organizationId) && !definition.SupportsOrganizationAssociation) return SetFailure(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not support organization association.", before, out failure);
            if (!string.IsNullOrWhiteSpace(governmentId) && !definition.SupportsGovernmentAssociation) return SetFailure(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not support government association.", before, out failure);
            if ((territoryIds ?? Array.Empty<string>()).Any(value => !string.IsNullOrWhiteSpace(value)) && !definition.SupportsTerritoryAssociation) return SetFailure(LocationOperationStatus.UnsupportedByDefinition, $"Location definition '{definition.Id}' does not support territory association.", before, out failure);
            if (!ValidateKnown(propertyId, knownPropertyIds, "property", before, out failure)) return false;
            if (!ValidateKnown(organizationId, knownOrganizationIds, "organization", before, out failure)) return false;
            if (!ValidateKnown(governmentId, knownGovernmentIds, "government", before, out failure)) return false;
            foreach (string territoryId in Clean(territoryIds))
            {
                if (!ValidateKnown(territoryId, knownTerritoryIds, "territory", before, out failure)) return false;
            }

            foreach (LocationAssociationReferenceData association in associations ?? Array.Empty<LocationAssociationReferenceData>())
            {
                if (association == null) continue;
                if (association.kind == LocationAssociationKind.Unknown || string.IsNullOrWhiteSpace(association.referenceId))
                {
                    return SetFailure(LocationOperationStatus.InvalidReference, "Location association references require a kind and reference ID.", before, out failure);
                }
            }

            return true;
        }

        private bool ValidateKnown(string value, ISet<string> known, string label, long before, out LocationOperationResult failure)
        {
            failure = null;
            if (!string.IsNullOrWhiteSpace(value) && known != null && known.Count > 0 && !known.Contains(value.Trim()))
            {
                return SetFailure(LocationOperationStatus.InvalidReference, $"Location references unknown {label} '{value}'.", before, out failure);
            }

            return true;
        }

        private bool SetFailure(LocationOperationStatus status, string message, long before, out LocationOperationResult failure)
        {
            failure = Fail(status, message, before);
            return false;
        }

        private bool TryGetDefinition(string id, out LocationDefinition definition, long before, out LocationOperationResult failure)
        {
            definition = null;
            failure = null;
            if (registry == null || !registry.TryGet(Normalize(id), out definition))
            {
                failure = Fail(LocationOperationStatus.MissingDefinition, $"Location definition '{id}' is not registered.", before);
                return false;
            }

            return true;
        }

        private bool TryDuplicate(string tx, string locationId, string operation, long before, out LocationOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(tx) || !transactionsById.TryGetValue(tx, out LocationTransactionRecordData found))
            {
                return false;
            }

            if (string.Equals(found.operation, operation, StringComparison.Ordinal) && string.Equals(found.locationId, locationId, StringComparison.Ordinal))
            {
                recordsById.TryGetValue(locationId, out LocationRecordData record);
                result = LocationOperationResult.Success(record == null ? null : BuildSnapshot(record), "Duplicate location transaction ignored.", before, before, duplicate: true);
                return true;
            }

            result = Fail(LocationOperationStatus.InvalidRequest, $"Transaction '{tx}' was already used for a different location operation.", before);
            return true;
        }

        private void RegisterTransaction(string tx, string operation, string locationId)
        {
            if (string.IsNullOrWhiteSpace(tx)) return;
            transactionsById[tx] = new LocationTransactionRecordData { transactionId = tx, operation = operation, locationId = locationId, revision = Revision + 1L };
        }

        private static void ApplyName(LocationRecordData record, LocationNameCategory category, string name)
        {
            if (category == LocationNameCategory.Official) record.officialName = name;
            else if (category == LocationNameCategory.Common) record.commonName = name;
            else if (category == LocationNameCategory.Alias)
            {
                record.aliases = Clean((record.aliases ?? Array.Empty<string>()).Concat(new[] { name }));
            }
        }

        private static void ApplyLifecycle(LocationRecordData record, LocationLifecycleState state, double worldTime)
        {
            record.lifecycleState = state;
            if (state == LocationLifecycleState.Destroyed || state == LocationLifecycleState.Historical || state == LocationLifecycleState.Removed)
            {
                record.endedWorldTime = worldTime;
            }
        }

        private static bool CanCreateLifecycle(LocationLifecycleState state)
        {
            return state == LocationLifecycleState.Proposed || state == LocationLifecycleState.Active || state == LocationLifecycleState.Inactive || state == LocationLifecycleState.Closed || state == LocationLifecycleState.Historical;
        }

        private static bool CanTransition(LocationLifecycleState from, LocationLifecycleState to)
        {
            if (!Enum.IsDefined(typeof(LocationLifecycleState), to) || to == LocationLifecycleState.Unknown) return false;
            if (from == LocationLifecycleState.Removed || from == LocationLifecycleState.Destroyed) return from == to;
            if (from == LocationLifecycleState.Historical && to == LocationLifecycleState.Active) return false;
            return true;
        }

        private static LocationSnapshot BuildSnapshot(LocationRecordData record) => new LocationSnapshot(record);
        private static LocationContainmentSnapshot BuildContainmentSnapshot(LocationContainmentLinkData link) => new LocationContainmentSnapshot(link);
        private static LocationSpatialRelationshipSnapshot BuildSpatialSnapshot(LocationSpatialRelationshipData relationship) => new LocationSpatialRelationshipSnapshot(relationship);
        private static LocationOperationResult Fail(LocationOperationStatus status, string message, long before) => LocationOperationResult.Failure(status, message, before);
        private static string BuildNameId(string locationId, string category, int sequence) => $"{locationId}.name.{category}.{Math.Max(1, sequence):0000}";
        private static string BuildContainmentLinkId(string parentId, string childId, LocationContainmentKind kind) => $"containment.{Normalize(parentId)}.{Normalize(childId)}.{kind.ToString().ToLowerInvariant()}";
        private static string BuildSpatialRelationshipId(string sourceId, string targetId, LocationSpatialRelationshipKind kind) => $"spatial.{Normalize(sourceId)}.{Normalize(targetId)}.{kind.ToString().ToLowerInvariant()}";
        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
