# Physical Client and Server Projects

Phase 5 Group 11 turns the repository into a Unity monorepo with two independent executable projects and one shared package source of truth.

## Open in Unity Hub

Add these as separate projects:

- `D:\Github\UnityIsekaiGame\Projects\Client`
- `D:\Github\UnityIsekaiGame\Projects\Server`

The repository root is no longer a Unity project. Opening either project generates only that project's Library, Temp, solution, and IDE files.

## Boundary

The Client project installs `com.thequantifier.isekai.client` and does not install `com.thequantifier.isekai.server`. It owns the playable Prototype Scene, cameras, input, presentation, UI, client editor tooling, and the full gameplay regression suite.

The Server project installs `com.thequantifier.isekai.server` and does not install `com.thequantifier.isekai.client`. It owns a stripped headless scene, dedicated-server build automation, server profile tests, and project-boundary tests. Its scene keeps authoritative simulation and network objects while excluding cameras, renderers, client bridges, UI, and development tools.

Both projects reference packages under the repository `Packages` folder using `file:../../../Packages/...` dependencies. Shared code or server-relevant data must be changed in those packages, never copied into both projects.

Client-only visual TerrainData remains under `Projects/Client/Assets/_Project/Prototype/Environment/Terrain/Data`. During extraction, collision-only TerrainData and lightweight terrain-tree collider proxies are generated under `Packages/com.thequantifier.isekai.content/Content/World/Terrain/ServerCollision`. This keeps heightfields and tree collision authoritative without copying terrain textures, foliage meshes, or other rendering dependencies into the server project.

## Build

Client menu:

`Tools > Unity Isekai Game > Networking > Build Windows Local Client`

Server menu:

`Tools > Unity Isekai Game > Build Windows Dedicated Server`

Command-line entry points:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\Github\UnityIsekaiGame\Projects\Client' -executeMethod UnityIsekaiGame.Editor.LocalNetworkBuildAutomation.BuildWindowsLocalClientCommandLine -logFile 'D:\Github\UnityIsekaiGame\Logs\client-build.log'

& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\Github\UnityIsekaiGame\Projects\Server' -executeMethod UnityIsekaiGame.ServerProject.Editor.ServerProjectBuildAutomation.BuildWindowsDedicatedServerCommandLine -logFile 'D:\Github\UnityIsekaiGame\Logs\server-build.log'
```

Outputs remain repository-level generated artifacts:

- `Builds/LocalClient/UnityIsekaiClient.exe`
- `Builds/LocalServer/UnityIsekaiServer.exe`

Each build is produced in a unique staging directory, checked for cross-side assemblies, and only then atomically replaces the previous successful output. A failed or interrupted build therefore cannot leave a partly updated runnable build. The server check also rejects client UI, development, and Input System assemblies.

## Scene maintenance

The authoritative server scene is generated from the client Prototype Scene with:

`Tools > Unity Isekai Game > Networking > Extract Dedicated Server Scene`

The extractor lives in `com.thequantifier.isekai.project-tools`. It preserves primitive and terrain collision object-by-object, redirects mesh colliders to deduplicated collision-only mesh assets while retaining their exact shapes, generates server-safe terrain/tree collision data, and synchronizes the generated scene into `Projects/Server/Assets/Scenes/ServerPrototypeScene.unity` while preserving that scene's meta GUID. It also emits `ServerPrototypeScene.inputs.sha256`; the Server build refuses to use the scene when any collision-relevant source input has changed. The build rewires a temporary scene copy and never mutates the tracked extracted scene. After intentional authority-scene changes, rerun the extraction command, both projects' tests, and both builds.

## Runtime authority and local admission

The dedicated server is the only world-simulation and persistence authority during a network session. A connected client switches its local persistence coordinator to replica mode, while standalone editor play remains authoritative for development. Connection requests require the matching protocol version, application build version, and the local authentication token generated under `.uig`.

`uig server start` waits until the authoritative scene and checkpoint have loaded and the transport is listening. `uig server status` distinguishes a merely running process from a ready server, and `uig client start` refuses to launch into the short startup window that would otherwise cause a connection timeout. Account authentication happens only through the client's login screen; launcher commands never accept or prefill a username or user ID.

The server owns a real-time idle-lifetime policy: when no remote transport client has been connected for 120 continuous seconds, it saves authoritative state, stops networking, and exits. App-authenticated clients on the login screen count as active connections, so account entry is not interrupted. The countdown resets after every connection and begins again only after the last remote client leaves.

## Live action and movement diagnostics

Reload the launcher and open tracing before or after starting the game:

```powershell
. $PROFILE
uig trace start
```

`uig trace start` does not restart the game. It opens the monitor, then enables runtime emission through the shared trace-control file. When launched before the game, it waits and attaches automatically. When recovering a closed monitor, it briefly gates emission until the monitor has established log offsets so the initial event burst is not lost.

Walk, sprint, release movement, and jump several times, then inspect the filtered logs:

```powershell
uig trace logs 200
uig trace status
uig trace summary
```

The monitor correlates FIFO client-send and server-receive records across movement, interaction, UI, inventory, combat, authentication, and system families. It displays raw one-way differences, a background probe baseline, and game response change after subtracting that baseline. `uig trace status` reports heartbeat freshness, rates, pending and expired correlations, log backlog, warning/critical counts, and server stalls. `uig trace summary` prints per-family mean, p95, and maximum values.

Movement transitions and correction changes are logged immediately; active movement is sampled every 0.2 seconds. Position reconciliation compares authority with the stored prediction for that same acknowledged sequence, shifts the still-unacknowledged prediction history by the resulting correction, and never waits for movement to stop. Jump intent travels atomically with its input sequence; the authoritative state reports the last processed and last executed jump sequences so a rejected prediction can end immediately. Matched samples are buffered to `Logs/Runtime/action-trace.csv`; restarting tracing archives the previous session under `Logs/Runtime/TraceArchive` with bounded retention. Tracing is disabled for ordinary launches.

## World-checkpoint capture diagnostics

World checkpoint capture advances at most one persistence work unit per server frame. Ordinary participants use one work unit; large social-interaction snapshots clone a bounded batch and continue on later frames, restarting from a new revision if the authoritative runtime changes during capture. Revision-aware participants reuse their last successfully written payload when their persisted state has not changed. Mutable runtime state is copied on the simulation thread; validation, deterministic ordering where supported, JSON serialization, envelope construction, and atomic disk writes run outside that frame path. This keeps the authoritative simulation responsive without allowing a background worker to read live mutable collections.

Every completed capture logs both an aggregate line and a raw sample line with UTC time and one `frame|participant|elapsedMs` entry per capture frame. Inspect the latest results with:

```powershell
uig server captures 5
```

The report includes total capture CPU time, mean, median, 0.1 ms mode bucket, p95, p99, maximum frame time, per-frame managed allocations, reused participant count, and the participant responsible for each maximum. Background write telemetry separately reports deferred serialization, envelope/checksum serialization, atomic-write time, total latency, and byte size. Raw samples are emitted to `Logs/Runtime/server.log`; before the next launch rotates that file, the launcher appends capture and write records to `Logs/Runtime/server-captures.log` so the history survives server restarts. Configurable warning and critical budgets emit explicit server warnings when capture time or allocation limits are crossed.

Player-profile autosaves are offset from world checkpoints. They compare detached authoritative state against the last queued snapshot, ignore insignificant transform drift, and do not increment revisions or enqueue writes when nothing changed. Profile serialization and atomic disk writes both run on the profile writer thread.
