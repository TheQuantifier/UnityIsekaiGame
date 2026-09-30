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

`uig server start` waits until the authoritative scene and checkpoint have loaded and the transport is listening. `uig server status` distinguishes a merely running process from a ready server, and `uig client <clientID> start` refuses to launch into the short startup window that would otherwise cause a connection timeout.
