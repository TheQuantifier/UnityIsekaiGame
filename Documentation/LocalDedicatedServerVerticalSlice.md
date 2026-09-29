# Local Dedicated Server Vertical Slice

## Goal

Use `Assets/_Project/Scenes/Prototype/PrototypeScene.unity` as the first playable client/server vertical slice. The server runs as a separate local process and owns mutable game state. The client owns input, camera, presentation, and menus, and requests state changes from the server.

No internet service is required. The initial transport is direct UDP over loopback (`127.0.0.1:7777`) using Netcode for GameObjects and Unity Transport.

## Audit baseline

- Unity Editor: `6000.5.4f1`
- Build scene: Prototype Scene only
- Runtime C# files: 1,087
- Existing networking layer: none
- Existing runtime assembly boundary: one broad gameplay assembly plus GameData and UI
- EditMode baseline: 1,363 passed, 0 failed
- PlayMode baseline: 5 passed, 0 failed

The existing `AuthoritativeCharacterSimulationDriver` already supports an externally driven authoritative clock. The major migration hotspot is `PrototypePersistenceServiceBehaviour`, which currently combines player persistence and world-service composition in one scene component.

## Ownership boundary

### Server owns

- World time and simulation ticks
- Actor identity and lifecycle
- Character resources, status effects, combat, and death
- Inventory, equipment, item identities, loot, and crafting results
- Quests, dialogue state, parties, organizations, economy, laws, and world persistence
- Validation of every gameplay command

### Client owns

- Input sampling and cursor state
- Camera and local presentation
- HUD and menus
- Connection/loading/error screens
- Prediction and interpolation where later justified

Opening a menu may block local input, but it must never pause the server or reset authoritative state.

## Group 1 deliverables

1. Add released Netcode for GameObjects and Unity Transport dependencies.
2. Establish separate protocol, client, and server assemblies.
3. Implement a versioned, size-limited connection payload.
4. Validate capacity, protocol compatibility, and duplicate player IDs on the server.
5. Provide explicit localhost client and dedicated-server lifecycle components.
6. Author a networking root into the Prototype Scene without changing existing gameplay behavior.
7. Add protocol and scene-structure tests and keep the full regression suite green.

## Migration sequence

1. **Handshake and process lifecycle:** prove that a client process can connect to a server process.
2. **Player session identity:** assign one authoritative player actor to each approved connection.
3. **Movement slice:** send input commands and replicate server-owned transforms.
4. **Vitals slice:** replicate health, stamina, mana, regeneration, and lifecycle state.
5. **Inventory slice:** replace direct UI mutations with commands and snapshots.
6. **Combat and quests:** move validation and outcomes behind server command handlers.
7. **Persistence:** make the server the only save/load writer.
8. **Physical project split:** extract shared packages and create thin Client and Server Unity project folders after the first end-to-end slice is stable.

The physical split is deliberately after the first working handshake. Copying the project first would duplicate the current coupled assembly and make every boundary change twice. Once the shared protocol and authoritative adapters are stable, both projects can consume the same versioned local packages without duplicating gameplay code.
