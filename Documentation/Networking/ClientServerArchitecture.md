# Client/Server Project Architecture

## Current decision

Phase 5 Group 10 completed the shared package extraction. The repository is still opened as one Unity project for the final compatibility pass, but reusable code and server-relevant content no longer live inside the project `Assets` tree. Group 11 can therefore create two thin Unity projects without copying gameplay code, protocol types, networking adapters, or shared definitions.

## Current repository layout

```text
UnityIsekaiGame/
|-- Assets/_Project/
|   |-- Prototype/                 client-only vertical-slice art and authoring
|   |-- Scenes/Prototype/          current vertical-slice world
|   |-- Editor/                    project bake/build automation
|   `-- Tests/                     shared regression suite
|-- Packages/
|   |-- com.thequantifier.isekai.protocol/
|   |-- com.thequantifier.isekai.simulation/
|   |-- com.thequantifier.isekai.networking/
|   `-- com.thequantifier.isekai.content/
|-- Documentation/
`-- Builds/
    |-- LocalClient/
    `-- LocalServer/
```

All four local packages use semantic version `0.1.0`. The root project consumes them through local UPM references, so Unity and IDE project generation treat package ownership explicitly.

## Package responsibilities

### `com.thequantifier.isekai.protocol`

Transport-independent connection, identity, session, command, validation, and persistence wire contracts. It may depend on serialization support, but never on simulation, Netcode behaviours, UI, client adapters, or server adapters.

### `com.thequantifier.isekai.simulation`

Reusable game definitions and gameplay domains: characters, inventory, equipment, combat, abilities, quests, parties, dialogue, persistence models, and other deterministic runtime state. It never references client UI or networking adapter assemblies.

### `com.thequantifier.isekai.networking`

Shared Netcode replication plus physically separate Client and Server adapter roots. Shared replication may depend on Protocol and Simulation. Client cannot reference Server; Server cannot reference Client or UI.

### `com.thequantifier.isekai.content`

Server-relevant authored definitions, configuration, generated records, and network prefabs. It contains no C# source. Large presentation assets and the current world remain project-owned so the future dedicated-server project does not import client art.

## Dependency direction

```text
Protocol        Simulation
     \          /
      Networking
          |
        Content

Networking/Client --> client executable and presentation
Networking/Server --> dedicated-server executable and persistence
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
7. Thin `Projects/Client` and `Projects/Server` Unity projects - Group 11.

Only Group 11 remains before the repository has physically separate client and server Unity projects.

## Group 11 target

```text
UnityIsekaiGame/
|-- Projects/
|   |-- Client/                    thin Unity presentation project
|   `-- Server/                    thin dedicated-server project
|-- Packages/                      single shared source of truth
|-- Documentation/
`-- Tools/
```

Both projects will reference the same packages by relative path. The Client project will own presentation assets and the vertical-slice scene. The Server project will own only headless bootstrap/configuration assets plus the shared server-relevant content it needs.

## Build outputs

- `Builds/LocalClient/UnityIsekaiClient.exe` is the player process.
- `Builds/LocalServer/UnityIsekaiServer.exe` is the dedicated authoritative process.

Build output is generated and ignored by Git. Source authority comes from package and project boundaries, not generated executables or IDE project files.
