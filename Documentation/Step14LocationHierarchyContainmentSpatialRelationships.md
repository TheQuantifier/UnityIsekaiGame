# Step 14.2 - Location Hierarchy, Containment, and Spatial Relationships

Feature 14.2 extends the Step 14 world-location runtime with runtime-authoritative location graph records.

## Authority Boundary

`LocationRuntime` remains the owner of stable location records, containment links, and spatial relationship records. Unity scene hierarchy, `Transform` parenting, terrain placement, and authored prototype objects are not authoritative for location containment.

The feature does not implement occupancy, current actor location, travel, pathfinding, doors, locks, interaction behavior, NPC routing, or multiplayer replication. Those systems may consume location graph projections later.

## Containment

Containment is represented by `LocationContainmentLinkData`.

Each active ordinary location has at most one active primary parent. Reparenting is atomic: the old link is ended and a new active link is created in one runtime mutation. Ended links remain in history for audit and save/restore.

Validation rejects:

- missing parent or child locations
- self-parenting
- unsupported parent/child category pairs
- multiple active parents
- cycles
- depth greater than `LocationRuntime.MaxContainmentDepth`
- corrupt transaction references

Queries are deterministic and return immutable snapshots:

- `GetRoots`
- `GetActiveParentLink`
- `GetChildren`
- `GetAncestors`
- `GetDescendants`
- `GetHierarchyPath`

## Spatial Relationships

Spatial relationships are represented by `LocationSpatialRelationshipData`.

They are descriptive only. They do not imply movement, path availability, route cost, travel mode, visibility line-of-sight, or interaction permission.

Relationships support:

- directional records
- symmetric records
- inverse lookup for directional relationships such as `Above` and `Below`
- visibility-safe normal and privileged projections

## Persistence

`LocationRuntimeSaveData` is versioned to schema 2. Schema 1 payloads remain valid as graph-empty payloads, while schema 2 saves include:

- location records
- location names
- location transaction records
- containment links
- spatial relationships

Prepare validation runs before commit and rejects corrupt graphs without mutating live runtime state.

## Prototype Graph

The prototype location seed now creates a representative hierarchy:

- world -> region
- region -> town and wilderness
- town -> district and buildings
- buildings -> offices, counters, and prison room
- wilderness -> dungeon entry

Representative spatial links describe nearby buildings, office/prison vertical placement, and dungeon membership in the wilderness complex.

## Test Coverage

Edit Mode tests cover:

- seeded graph validation
- deterministic traversal
- cycle prevention
- active parent constraints
- reparent history
- spatial inverse and symmetric queries
- persistence round trip
- corrupt restore rejection
- immutable snapshots
- visibility-safe projection

Test Lab suite:

`feature.14.2.location-hierarchy-containment-spatial-relationships`

It contains twelve scenarios for readiness, traversal, cycle prevention, parent constraints, reparenting, spatial directionality, non-routing boundary, preview/idempotence, persistence, corrupt restore rejection, visibility, and fixture snapshot rollback.
