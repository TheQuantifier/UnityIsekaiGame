# Local Dedicated Server Vertical Slice

## Goal

Use `Projects/Client/Assets/_Project/Scenes/Prototype/PrototypeScene.unity` as the first playable client/server vertical slice. The server runs from the independent `Projects/Server` Unity project as a separate local process and owns mutable game state. The client owns input, camera, presentation, and menus, and requests state changes from the server.

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
8. Support explicit command-line server/client startup with endpoint and identity overrides.
9. Produce repeatable Windows server and client builds and verify a real loopback handshake between separate processes.

## Build and run

Windows Dedicated Server Build Support for Unity `6000.5.4f1` must be installed in Unity Hub. Visual Studio is not required to run either executable.

Add and open `Projects/Client` and `Projects/Server` as separate projects in Unity Hub. Build from the corresponding Unity Editor menu:

- Server project: `Tools > Unity Isekai Game > Build Windows Dedicated Server`
- Client project: `Tools > Unity Isekai Game > Networking > Build Windows Local Client`

The outputs are intentionally ignored by Git:

- `Builds/LocalServer/UnityIsekaiServer.exe`
- `Builds/LocalClient/UnityIsekaiClient.exe`

Start the server first:

```powershell
cd Builds/LocalServer
.\UnityIsekaiServer.exe -batchmode -nographics -logFile server.log
```

A dedicated-server build starts automatically on `0.0.0.0:7777`. Optional overrides are `--listen-address`, `--server-port`, and `--max-players`:

```powershell
.\UnityIsekaiServer.exe -batchmode -nographics --listen-address 127.0.0.1 --server-port 7788 --max-players 4 -logFile server.log
```

Start a client in a second terminal. The `--local-client` flag is deliberately required so an ordinary game launch remains inert until a connection screen is added:

```powershell
cd Builds/LocalClient
.\UnityIsekaiClient.exe -batchmode -nographics --local-client --server-address 127.0.0.1 --server-port 7777 --player-id local-player -logFile client.log
```

The server log reports `Local server is listening`, followed by the approved client ID and player ID. The client log reports `Connected`. Each build folder is a portable unit; keep its executable and generated data/dependency folders together.

## Current boundary

Group 1 proves process separation, transport startup, versioned admission, capacity checks, unique player identity, disconnect cleanup, and a real client/server handshake. It does not yet replicate the player actor or gameplay state. Those authoritative adapters begin in Group 2; until then, the existing Prototype Scene gameplay still runs locally inside each process.

## Group 1 verification

Verified on September 28, 2026 with Unity `6000.5.4f1`:

- EditMode: 1,383 passed, 0 failed.
- PlayMode: 5 passed, 0 failed.
- Windows dedicated-server build: succeeded.
- Windows client build: succeeded.
- Separate-process smoke test: server bound UDP `127.0.0.1:7777`; client `e2e-player` connected and was approved.
- Headless logs: no missing-script, serialization-layout, networking, exception, or error warnings.

## Migration sequence

1. **Handshake and process lifecycle:** prove that a client process can connect to a server process.
2. **Player session identity:** assign one authoritative player actor to each approved connection.
3. **Movement slice:** send input commands and replicate server-owned transforms.
4. **Vitals slice:** replicate health, stamina, mana, regeneration, and lifecycle state.
5. **Inventory slice:** replace direct UI mutations with commands and snapshots.
6. **Combat and quests:** move validation and outcomes behind server command handlers.
7. **Persistence:** make the server the only save/load writer.
8. **Physical project split:** complete; shared packages now feed independent Client and Server Unity projects.

The physical split followed the first working handshake. Both projects now consume the same versioned local packages without duplicating gameplay code.
