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

## Scene maintenance

The authoritative server scene is generated from the client Prototype Scene with:

`Tools > Unity Isekai Game > Networking > Extract Dedicated Server Scene`

The extractor lives in `com.thequantifier.isekai.project-tools`. After intentional authority-scene changes, regenerate the scene, copy the result into `Projects/Server/Assets/Scenes/ServerPrototypeScene.unity` while preserving its meta GUID, then rerun both projects' tests and builds.
