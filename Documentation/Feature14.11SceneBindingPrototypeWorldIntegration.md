# Feature 14.11 - Scene Binding and Prototype World Integration

Feature 14.11 adds the transient bridge between the scene-independent Step 14 world/location/travel runtimes and Unity scene objects.

The core rule is unchanged: `LocationRuntime`, `EntityLocationRuntime`, `InteractionPointRuntime`, `LocationConnectionRuntime`, `LocationRouteRuntime`, `TravelJourneyRuntime`, and `PoliticalTravelRuntime` own authoritative state. Scene bindings own live `GameObject`, `Transform`, collider, prompt, and visual synchronization only.

## Authority Boundary

Scene binding supports this flow:

- Logical simulation -> `WorldSceneBindingRuntime` -> Unity presentation.
- Unity interaction/input -> binding request -> owning Step 14 runtime operation -> presentation sync.

It explicitly rejects this flow:

- `Transform` changed -> authoritative logical location silently changes.

Designer movement or scene hierarchy edits never rewrite logical location hierarchy, occupancy, routes, doors, checkpoints, or journeys. Use explicit transition requests when physical play should update logical state.

## Runtime

`WorldSceneBindingRuntime` is a transient registry for loaded scene bindings. It stores:

- logical ID plus category -> live binding component;
- stable scene binding key -> live binding component;
- scene key/world ID diagnostics;
- duplicate primary binding detection;
- missing optional versus required binding diagnostics;
- presentation synchronization from authoritative runtimes.

The registry is not persisted. Save/load restores authoritative Step 14 runtimes first, then scene bindings re-register and call `SyncAllFromAuthoritative`.

## Binding Components

The scene layer uses focused MonoBehaviours:

- `WorldSceneBindingBootstrap`: configures a scene registry and sync mode.
- `LocationSceneBinding`: binds a `LocationId` to an anchor or room/building marker.
- `SpawnAnchorSceneBinding`: marks a spawn or placement anchor for a logical location.
- `InteractionPointSceneBinding`: implements `IInteractable` and routes scene interaction to `InteractionPointRuntime`.
- `ConnectionSceneBinding`: implements `IInteractable` and routes open/close/traverse requests to `LocationConnectionRuntime`.
- `WorldEntitySceneBinding`: binds a person/body/item scene object to authoritative placement.
- `RouteSegmentSceneBinding`: binds route presentation to `LocationRouteRuntime`.
- `JourneySceneBinding`: binds journey presentation to `TravelJourneyRuntime`.
- `CheckpointSceneBinding`: binds checkpoint presentation to `PoliticalTravelRuntime`.

Each binding has a stable logical ID and optional scene binding key. GameObject names are labels only.

## Readiness and Diagnostics

Bindings report one of:

- `Unregistered`
- `WaitingForWorld`
- `WaitingForLogicalRecord`
- `Bound`
- `Degraded`
- `Duplicate`
- `Invalid`
- `Disposed`

Required missing records and duplicate primary bindings are validation errors. Optional missing records are warnings. Auxiliary bindings can share a logical ID; duplicate primary bindings are deterministic and only one primary remains bound.

Use `Tools > World Locations > Scene Binding > Validate Current Scene` to inspect loaded scene bindings before play.

## Production and Prototype Bootstrap

Production binding never creates logical records just because a scene object exists.

Prototype and Test Lab fixtures may explicitly seed logical records through the existing Step 14 factory/runtime APIs. This is a development fixture operation, not a scene scan.

## Presentation Synchronization

`SyncAllFromAuthoritative` refreshes scene presentation from owning runtimes. Initial sync does not replay historical animation or events.

Examples:

- Entity bindings materialize to the spawn anchor for their authoritative active placement when one is loaded, otherwise to the location anchor.
- Connection bindings update colliders from authoritative open/closed/blocked state.
- Interaction bindings expose prompt/readiness but do not own services.
- Route, journey, and checkpoint bindings expose presentation mappings without mutating route or political-travel records.

The prototype travel panel only offers public destinations that have a loaded location or spawn anchor, so a completed logical journey cannot leave the visible player at the previous destination.

## Manual PrototypeScene Binding Plan

Because scene YAML edits are intentionally avoided, add these components in the Unity Inspector when wiring the current prototype markers.

Add `WorldSceneBindingBootstrap` to `PrototypeScene > Test Infrastructure` or the root runtime/bootstrap object.

Recommended bootstrap fields:

- World ID: `local-world`
- Scene Key: `scene.prototype`
- Mode: production binding for ordinary play, development/prototype fixture only for explicit fixture creation
- Sync On Enable: enabled

Add `LocationSceneBinding` to scene anchors or parent objects:

- Town: logical ID `location.prototype.town`, binding key `prototype.scene.location.town`
- Adventurer Guild: logical ID `location.prototype.adventurers-guild`, binding key `prototype.scene.location.guild`
- Merchant Counter: logical ID `location.prototype.merchant-counter`, binding key `prototype.scene.merchant-counter`
- Mayor Office: logical ID `location.prototype.mayor-office`, binding key `prototype.scene.location.mayor-office`
- Guild Head Office: logical ID `location.prototype.guildmaster-office`, binding key `prototype.scene.location.guild-head-office`
- Civic Office: logical ID `location.prototype.civic-office`, binding key `prototype.scene.location.civic-office`
- Basement Prison: logical ID `location.prototype.basement-prison`, binding key `prototype.scene.location.basement-prison`

Add `InteractionPointSceneBinding` to the existing interaction marker objects:

- `Gameplay > Interaction Points > Adventurer Guild Counter`
  - Interaction Point ID: `interaction-point.prototype.adventurer-guild-counter`
  - Binding Key: `prototype.scene.interaction.adventurer-guild-counter`
  - Host Location ID: `location.prototype.adventurers-guild`
- `Gameplay > Interaction Points > Merchant Guild Counter`
  - Interaction Point ID: `interaction-point.prototype.merchant-guild-counter`
  - Binding Key: `prototype.scene.interaction.merchant-guild-counter`
  - Host Location ID: `location.prototype.merchant-counter`
- `Gameplay > Interaction Points > Mayor Desk`
  - Interaction Point ID: `interaction-point.prototype.mayor-desk`
  - Binding Key: `prototype.scene.interaction.mayor-desk`
  - Host Location ID: `location.prototype.mayor-office`
- `Gameplay > Interaction Points > Guild Head Desk`
  - Interaction Point ID: `interaction-point.prototype.guild-head-desk`
  - Binding Key: `prototype.scene.interaction.guild-head-desk`
  - Host Location ID: `location.prototype.guildmaster-office`
- `Gameplay > Interaction Points > City Office Records Desk`
  - Interaction Point ID: `interaction-point.prototype.city-records-desk`
  - Binding Key: `prototype.scene.interaction.city-records-desk`
  - Host Location ID: `location.prototype.civic-office`
- `Gameplay > Interaction Points > Prison Cell`
  - Interaction Point ID: `interaction-point.prototype.prison-cell`
  - Binding Key: `prototype.scene.interaction.prison-cell`
  - Host Location ID: `location.prototype.basement-prison`

Add `ConnectionSceneBinding` to door, passage, or trigger objects:

- Town/Guild Entrance: `location-connection.prototype.town-guild-entrance`, binding key `prototype.connection.town-guild`
- Market/Merchant Counter: `location-connection.prototype.market-merchant-counter`, binding key `prototype.connection.market-merchant`
- Guild Head Office Door: `location-connection.prototype.guild-head-office`, binding key `prototype.connection.guild-head-office`
- Mayor Office Door: `location-connection.prototype.mayor-office`, binding key `prototype.connection.mayor-door`
- Records Office Door: `location-connection.prototype.records-office`, binding key `prototype.connection.records-door`
- Prison Cell Door: `location-connection.prototype.prison-cell-door`, binding key `prototype.connection.prison-cell`

Assign the controlled collider on each `ConnectionSceneBinding` when the collider should block movement while the authoritative connection is closed or blocked.

Add `WorldEntitySceneBinding` to character scene roots:

- Prototype Player body: entity ID `body.prototype.player`, binding key `prototype.scene.entity.player`
- Guild master body: entity ID `body.prototype.guildmaster`, binding key `prototype.scene.entity.guildmaster`
- Merchant body: entity ID `body.prototype.merchant`, binding key `prototype.scene.entity.merchant`
- Prisoner body: entity ID `body.prototype.prisoner`, binding key `prototype.scene.entity.prisoner`

Use body IDs for physical scene representation. Person IDs remain the stable identity that may span bodies.

Optional presentation markers:

- Route segment marker: `route-segment.prototype.town-market-street`
- Checkpoint marker: authored checkpoint ID from `PoliticalTravelRuntime`
- Journey marker: active `JourneyId` from `TravelJourneyRuntime`

## Manual Validation

In Play Mode:

1. Run the Feature 14.11 Test Lab automation suite.
2. Validate current scene bindings from `Tools > World Locations > Scene Binding > Validate Current Scene`.
3. Confirm the player binding resolves to `body.prototype.player` and materializes from authoritative placement.
4. Interact with the Adventurer Guild Counter and Merchant Guild Counter and confirm the logical interaction point resolves.
5. Interact with Mayor Desk, Guild Head Desk, Records Desk, and Prison Cell markers and confirm each remains separate from location and connection records.
6. Toggle a bound door open/closed/locked through Test Lab/runtime actions and confirm the collider follows the authoritative connection state.
7. Attempt a denied transition and confirm authoritative entity placement does not change.
8. Save and reload in a nondefault location, then confirm restore completes before scene presentation rebinds.

## Deferred Scope

Feature 14.11 does not implement final world streaming, NavMesh authority, NPC pathfinding, polished UI, multiplayer replication, VR input, quest/dialogue content, or final door animations. Those systems should consume the binding layer rather than replace it.
