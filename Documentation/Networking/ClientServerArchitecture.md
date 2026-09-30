# Client/Server Project Architecture

## Current decision

Phase 5 Group 11 completed the physical project split. The repository contains separate Client and Server Unity projects that consume one shared set of versioned local packages. Gameplay code, protocol types, network replication, authority adapters, and server-relevant definitions are not copied between projects.

## Current repository layout

```text
UnityIsekaiGame/
|-- Projects/
|   |-- Client/
|   |   |-- Assets/_Project/       client art, presentation, scenes, editor tools, tests
|   |   |-- Packages/              client manifest; includes Client, excludes Server
|   |   `-- ProjectSettings/
|   `-- Server/
|       |-- Assets/Scenes/         stripped headless vertical-slice scene
|       |-- Assets/Editor/         server build automation and boundary tests
|       |-- Packages/              server manifest; includes Server, excludes Client
|       `-- ProjectSettings/
|-- Packages/
|   |-- com.thequantifier.isekai.protocol/
|   |-- com.thequantifier.isekai.simulation/
|   |-- com.thequantifier.isekai.networking/
|   |-- com.thequantifier.isekai.client/
|   |-- com.thequantifier.isekai.server/
|   |-- com.thequantifier.isekai.content/
|   |-- com.thequantifier.isekai.project-tools/
|   `-- com.thequantifier.scene-zone-tool/
|-- Documentation/
`-- Builds/
    |-- LocalClient/
    `-- LocalServer/
```

The Isekai packages use semantic version `0.1.0`; Scene Zone Tool retains its released `1.0.3` version. Each Unity project consumes only its permitted adapters through relative local UPM references.

## Package responsibilities

### `com.thequantifier.isekai.protocol`

Transport-independent connection, identity, session, command, validation, and persistence wire contracts. It may depend on serialization support, but never on simulation, Netcode behaviours, UI, client adapters, or server adapters.

### `com.thequantifier.isekai.simulation`

Reusable game definitions and gameplay domains: characters, inventory, equipment, combat, abilities, quests, parties, dialogue, persistence models, and other deterministic runtime state. It never references client UI or networking adapter assemblies.

### `com.thequantifier.isekai.networking`

Shared Netcode replication used by both executables. It may depend on Protocol and Simulation, but contains neither client nor server adapters.

### `com.thequantifier.isekai.client`

Client connection lifecycle, input and presentation bridges, and the user interface. It is installed only in `Projects/Client` and cannot reference Server.

### `com.thequantifier.isekai.server`

Dedicated-server startup, admission, authoritative command handling, world/session adapters, and server-owned persistence. It is installed only in `Projects/Server` and cannot reference Client or UI.

### `com.thequantifier.isekai.content`

Server-relevant authored definitions, configuration, generated records, and network prefabs. It contains no C# source. Large presentation assets and the current world remain project-owned so the future dedicated-server project does not import client art.

## Dependency direction

```text
Protocol        Simulation
     \          /
      Networking
          |
        Content

Networking --> Client package --> Client project
Networking --> Server package --> Server project
```

The Content package depends on Simulation and Networking because its Unity assets serialize components and definition types from those packages. That is an asset serialization dependency, not an authority inversion.

## Enforced rules

- Protocol never references gameplay, Netcode behaviours, Client, Server, or UI.
- Simulation never references Client, Server, shared replication, Protocol, or UI.
- Shared networking never references Client, Server, gameplay presentation, or UI.
- Client never references Server.
- Server never references Client or UI.
- Content contains no runtime code.
- Legacy `Assets/_Project/Runtime` and `Assets/_Project/Content` cannot regain source or authored content.
- Package versions and inter-package dependencies are checked by the project structure validator.
- Known scene, definition, and network-prefab GUIDs are tested so serialized references survive extraction.

## Extraction sequence

1. Physical protocol, shared replication, client, server, and UI roots - complete.
2. Server-authoritative inventory commands and snapshots - complete in Group 6.
3. Combat and ability command boundaries - complete in Group 7.
4. Quest, party, and interaction command boundaries - complete in Group 8.
5. Server-only persistence ownership and player-session restore - complete in Group 9.
6. Versioned packages and explicit content boundaries - complete in Group 10.
7. Thin `Projects/Client` and `Projects/Server` Unity projects - complete in Group 11.

## Physical project result

```text
UnityIsekaiGame/
|-- Projects/
|   |-- Client/                    thin Unity presentation project
|   `-- Server/                    thin dedicated-server project
|-- Packages/                      single shared source of truth
|-- Documentation/
`-- Tools/
```

Both projects reference the same repository packages by relative path. The Client owns presentation assets and the Prototype Scene. The Server owns only its headless bootstrap scene/configuration and imports shared server-relevant content. The server scene is extracted from the vertical slice with cameras, renderers, UI, client adapters, development tools, and visual collision removed.

Open these folders separately in Unity Hub:

- `Projects/Client`
- `Projects/Server`

Do not open the repository root as a Unity project.

## Build outputs

- `Builds/LocalClient/UnityIsekaiClient.exe` is the player process.
- `Builds/LocalServer/UnityIsekaiServer.exe` is the dedicated authoritative process.

Build output is generated and ignored by Git. Source authority comes from package and project boundaries, not generated executables or IDE project files.
