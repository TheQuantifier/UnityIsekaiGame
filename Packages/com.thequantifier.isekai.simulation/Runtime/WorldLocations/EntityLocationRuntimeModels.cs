using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.WorldLocations
{
    [Serializable]
    public sealed class EntityLocationReferenceData
    {
        public LocationOccupantEntityType entityType;
        public string entityId;
        public string worldId;

        public string StableKey => EntityLocationReferenceKey.Build(entityType, entityId, worldId);

        public EntityLocationReferenceData Clone()
        {
            return new EntityLocationReferenceData
            {
                entityType = entityType,
                entityId = entityId ?? string.Empty,
                worldId = worldId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class EntityPlacementRecordData
    {
        public string placementId;
        public EntityLocationReferenceData entity = new EntityLocationReferenceData();
        public string exactLocationId;
        public string worldId;
        public EntityPlacementCategory category = EntityPlacementCategory.Present;
        public EntityPlacementLifecycleState lifecycleState = EntityPlacementLifecycleState.Active;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public LocationVisibility visibility = LocationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string transitionId;
        public long revision = 1L;

        public EntityPlacementRecordData Clone()
        {
            return new EntityPlacementRecordData
            {
                placementId = placementId ?? string.Empty,
                entity = entity?.Clone() ?? new EntityLocationReferenceData(),
                exactLocationId = exactLocationId ?? string.Empty,
                worldId = worldId ?? string.Empty,
                category = category,
                lifecycleState = lifecycleState,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                transitionId = transitionId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EntityLocationTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string entityKey;
        public string placementId;
        public long revision;

        public EntityLocationTransactionRecordData Clone()
        {
            return new EntityLocationTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                entityKey = entityKey ?? string.Empty,
                placementId = placementId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EntityLocationCapacityRuleData
    {
        public string locationId;
        public int maxDirectOccupants = -1;
        public LocationOccupantEntityType[] allowedEntityTypes = Array.Empty<LocationOccupantEntityType>();

        public EntityLocationCapacityRuleData Clone()
        {
            return new EntityLocationCapacityRuleData
            {
                locationId = locationId ?? string.Empty,
                maxDirectOccupants = maxDirectOccupants,
                allowedEntityTypes = (allowedEntityTypes ?? Array.Empty<LocationOccupantEntityType>()).ToArray()
            };
        }
    }

    [Serializable]
    public sealed class EntityPersonBodyBindingData
    {
        public string personId;
        public string activeBodyId;
        public bool bodyDestroyed;
        public string sourceId;

        public EntityPersonBodyBindingData Clone()
        {
            return new EntityPersonBodyBindingData
            {
                personId = personId ?? string.Empty,
                activeBodyId = activeBodyId ?? string.Empty,
                bodyDestroyed = bodyDestroyed,
                sourceId = sourceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class EntityLocationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<EntityPlacementRecordData> placements = new List<EntityPlacementRecordData>();
        public List<EntityLocationTransactionRecordData> transactions = new List<EntityLocationTransactionRecordData>();
        public List<EntityLocationReferenceData> knownEntities = new List<EntityLocationReferenceData>();
        public List<EntityLocationReferenceData> inventoryHeldEntities = new List<EntityLocationReferenceData>();
        public List<EntityLocationCapacityRuleData> capacityRules = new List<EntityLocationCapacityRuleData>();
        public List<EntityPersonBodyBindingData> personBodyBindings = new List<EntityPersonBodyBindingData>();

        public EntityLocationRuntimeSaveData Clone()
        {
            return new EntityLocationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                placements = (placements ?? new List<EntityPlacementRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                transactions = (transactions ?? new List<EntityLocationTransactionRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                knownEntities = (knownEntities ?? new List<EntityLocationReferenceData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                inventoryHeldEntities = (inventoryHeldEntities ?? new List<EntityLocationReferenceData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                capacityRules = (capacityRules ?? new List<EntityLocationCapacityRuleData>()).Where(value => value != null).Select(value => value.Clone()).ToList(),
                personBodyBindings = (personBodyBindings ?? new List<EntityPersonBodyBindingData>()).Where(value => value != null).Select(value => value.Clone()).ToList()
            };
        }
    }

    public sealed class EntityPlacementSnapshot
    {
        private readonly EntityPlacementRecordData data;

        public EntityPlacementSnapshot(EntityPlacementRecordData record)
        {
            data = record?.Clone() ?? new EntityPlacementRecordData();
        }

        public string PlacementId => data.placementId ?? string.Empty;
        public EntityLocationReferenceData Entity => data.entity?.Clone() ?? new EntityLocationReferenceData();
        public LocationOccupantEntityType EntityType => data.entity?.entityType ?? LocationOccupantEntityType.Unknown;
        public string EntityId => data.entity?.entityId ?? string.Empty;
        public string EntityWorldId => data.entity?.worldId ?? string.Empty;
        public string EntityKey => EntityLocationReferenceKey.Build(EntityType, EntityId, EntityWorldId);
        public string ExactLocationId => data.exactLocationId ?? string.Empty;
        public string WorldId => data.worldId ?? string.Empty;
        public EntityPlacementCategory Category => data.category;
        public EntityPlacementLifecycleState LifecycleState => data.lifecycleState;
        public double StartWorldTime => data.startWorldTime;
        public double EndWorldTime => data.endWorldTime;
        public LocationVisibility Visibility => data.visibility;
        public string SourceEventId => data.sourceEventId ?? string.Empty;
        public string SourceRecordId => data.sourceRecordId ?? string.Empty;
        public string ProvenanceId => data.provenanceId ?? string.Empty;
        public string TransitionId => data.transitionId ?? string.Empty;
        public long Revision => data.revision;
        public bool IsActive => LifecycleState == EntityPlacementLifecycleState.Active;
        public EntityPlacementRecordData ToSaveData() => data.Clone();
    }

    public sealed class EntityLocationOperationResult
    {
        private EntityLocationOperationResult(EntityLocationOperationStatus status, string message, EntityPlacementSnapshot placement, EntityPlacementSnapshot previousPlacement, HierarchyTransitionDiff transitionDiff, bool duplicate, bool preview, long before, long after)
        {
            Status = status;
            Message = message ?? string.Empty;
            Placement = placement;
            PreviousPlacement = previousPlacement;
            TransitionDiff = transitionDiff ?? HierarchyTransitionDiff.Empty;
            Duplicate = duplicate;
            Preview = preview;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public EntityLocationOperationStatus Status { get; }
        public string Message { get; }
        public EntityPlacementSnapshot Placement { get; }
        public EntityPlacementSnapshot PreviousPlacement { get; }
        public HierarchyTransitionDiff TransitionDiff { get; }
        public bool Succeeded => Status == EntityLocationOperationStatus.Succeeded || Status == EntityLocationOperationStatus.Preview || Status == EntityLocationOperationStatus.Duplicate;
        public bool Duplicate { get; }
        public bool Preview { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }

        public static EntityLocationOperationResult Success(EntityPlacementSnapshot placement, string message, long before, long after, EntityPlacementSnapshot previousPlacement = null, HierarchyTransitionDiff diff = null, bool duplicate = false, bool preview = false)
        {
            return new EntityLocationOperationResult(preview ? EntityLocationOperationStatus.Preview : duplicate ? EntityLocationOperationStatus.Duplicate : EntityLocationOperationStatus.Succeeded, message, placement, previousPlacement, diff, duplicate, preview, before, after);
        }

        public static EntityLocationOperationResult Failure(EntityLocationOperationStatus status, string message, long before)
        {
            return new EntityLocationOperationResult(status, message, null, null, HierarchyTransitionDiff.Empty, false, false, before, before);
        }
    }

    public sealed class EntityLocationResolutionResult
    {
        private EntityLocationResolutionResult(EntityPhysicalLocationResolutionStatus status, EntityPlacementSnapshot placement, EntityPlacementSnapshot bodyPlacement, string message)
        {
            Status = status;
            Placement = placement;
            BodyPlacement = bodyPlacement;
            Message = message ?? string.Empty;
        }

        public EntityPhysicalLocationResolutionStatus Status { get; }
        public EntityPlacementSnapshot Placement { get; }
        public EntityPlacementSnapshot BodyPlacement { get; }
        public string Message { get; }
        public bool Succeeded => Status == EntityPhysicalLocationResolutionStatus.ResolvedExact || Status == EntityPhysicalLocationResolutionStatus.ResolvedThroughBody;
        public string LocationId => Placement?.ExactLocationId ?? BodyPlacement?.ExactLocationId ?? string.Empty;

        public static EntityLocationResolutionResult Exact(EntityPlacementSnapshot placement, string message = "Entity exact location resolved.")
        {
            return new EntityLocationResolutionResult(EntityPhysicalLocationResolutionStatus.ResolvedExact, placement, null, message);
        }

        public static EntityLocationResolutionResult ThroughBody(EntityPlacementSnapshot bodyPlacement, string message = "Person location resolved through active body.")
        {
            return new EntityLocationResolutionResult(EntityPhysicalLocationResolutionStatus.ResolvedThroughBody, null, bodyPlacement, message);
        }

        public static EntityLocationResolutionResult Failure(EntityPhysicalLocationResolutionStatus status, string message)
        {
            return new EntityLocationResolutionResult(status, null, null, message);
        }
    }

    public sealed class LocationOccupancySnapshot
    {
        public LocationOccupancySnapshot(string locationId, bool recursive, IEnumerable<EntityPlacementSnapshot> placements)
        {
            LocationId = locationId ?? string.Empty;
            Recursive = recursive;
            Placements = (placements ?? Array.Empty<EntityPlacementSnapshot>())
                .Where(value => value != null)
                .OrderBy(value => value.EntityKey, StringComparer.Ordinal)
                .ThenBy(value => value.PlacementId, StringComparer.Ordinal)
                .ToArray();
        }

        public string LocationId { get; }
        public bool Recursive { get; }
        public IReadOnlyList<EntityPlacementSnapshot> Placements { get; }
        public int Count => Placements.Count;
    }

    public sealed class HierarchyTransitionDiff
    {
        public static readonly HierarchyTransitionDiff Empty = new HierarchyTransitionDiff(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        public HierarchyTransitionDiff(IEnumerable<string> entered, IEnumerable<string> exited, IEnumerable<string> shared, IEnumerable<string> path)
        {
            EnteredLocationIds = Clean(entered);
            ExitedLocationIds = Clean(exited);
            SharedLocationIds = Clean(shared);
            DestinationPathLocationIds = Clean(path);
        }

        public IReadOnlyList<string> EnteredLocationIds { get; }
        public IReadOnlyList<string> ExitedLocationIds { get; }
        public IReadOnlyList<string> SharedLocationIds { get; }
        public IReadOnlyList<string> DestinationPathLocationIds { get; }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class EntityPlacementRequest
    {
        public string transactionId;
        public string placementId;
        public EntityLocationReferenceData entity;
        public string exactLocationId;
        public EntityPlacementCategory category = EntityPlacementCategory.Present;
        public double worldTime;
        public LocationVisibility visibility = LocationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class EntityRelocationRequest
    {
        public string transactionId;
        public string newPlacementId;
        public EntityLocationReferenceData entity;
        public string expectedOriginLocationId;
        public string destinationLocationId;
        public EntityPlacementCategory category = EntityPlacementCategory.Present;
        public double worldTime;
        public LocationVisibility visibility = LocationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public sealed class EntityUnplacementRequest
    {
        public string transactionId;
        public EntityLocationReferenceData entity;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long expectedRevision = -1L;
        public bool preview;
    }

    public static class EntityLocationReferenceKey
    {
        public static string Build(LocationOccupantEntityType entityType, string entityId, string worldId)
        {
            string type = entityType == LocationOccupantEntityType.Unknown ? "Unknown" : entityType.ToString();
            string id = string.IsNullOrWhiteSpace(entityId) ? string.Empty : entityId.Trim();
            string world = string.IsNullOrWhiteSpace(worldId) ? string.Empty : worldId.Trim();
            return $"{type}:{world}:{id}";
        }
    }
}
