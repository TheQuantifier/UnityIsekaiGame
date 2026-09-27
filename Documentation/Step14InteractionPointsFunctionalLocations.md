# Step 14.4 Interaction Points and Functional Locations

Feature 14.4 adds authoritative logical interaction points on top of the Step 14 location and occupancy model.

Interaction points are stable world records such as an adventurer guild counter, merchant counter, mayor desk, records desk, prison cell point, quest board, storage access point, or workstation. They are not scene objects and they do not own the systems they route to.

## Authority Boundary

- `LocationRuntime` owns places, containment, spatial relationships, and location lifecycle.
- `EntityLocationRuntime` owns physical presence and occupancy.
- `InteractionPointRuntime` owns interaction point identity, host assignment, service bindings, subject links, provider assignments, reservations, sessions, visibility, and scene binding keys.
- Destination systems such as organizations, law, quests, inventory, crafting, business, justice, knowledge, and social simulation remain the owners of their own records.

An interaction point can validate that a service route is available and return a destination runtime reference. It must not directly mutate destination runtime state.

## Runtime Model

Each interaction point has:

- Stable interaction point ID.
- Definition ID.
- Active host location ID.
- Host assignment history.
- Bound service definition IDs.
- Visibility.
- Optional scene binding key.
- Capacity and exclusive-use state.
- Subject links to external records.
- Provider assignments.
- Reservations and use sessions.
- Revision and transaction history.

Definitions describe allowed host location categories, supported service categories, subject link roles, provider requirements, presence rules, capacity, and destination runtime routing.

## Scene Binding

Scene objects should bind to interaction points by stable key or stable interaction point ID. The scene object is a view/control surface only. Save data stores keys, not `GameObject` or `Transform` identity.

## Persistence

`InteractionPointPersistenceParticipant` captures and restores the runtime as a shared-world participant. Restore validates the whole graph before commit:

- Referenced point definitions must exist.
- Referenced services must exist and be compatible.
- Host locations must exist and support interaction points.
- Active host assignments must match active point hosts.
- Subject links, providers, reservations, and sessions must reference existing points.
- Entity references used by providers and sessions must resolve through `EntityLocationRuntime`.

Invalid payloads are rejected before live runtime mutation.

## Prototype Coverage

The prototype factory seeds representative logical points for the current town/guild flow:

- Adventurer guild counter.
- Merchant guild counter.
- Mayor desk.
- Guild head desk.
- City records desk.
- Prison cell interaction point.
- Quest board.
- Shop counter.
- Guild storage access.
- Prototype workstation.

These are enough for future prototype scene markers to bind to the logical model without making the scene authoritative.
