# Client/Server Project Architecture

## Decision

The repository remains one Unity project during the authority migration, but source code is physically divided into Shared, Client, and Server roots. This is an intentional monorepo architecture, not a permanent requirement that both executables share one project.

Creating two complete Unity projects before inventory, combat, quests, and persistence are server-owned would duplicate the coupled Prototype Scene and its content. Every boundary correction would then need to be made twice. The safer extraction point is after those mutable systems have server command/snapshot APIs.

## Current repository layout

```text
UnityIsekaiGame/
├─ Assets/_Project/
│  ├─ Runtime/
│  │  ├─ Shared/
│  │  │  └─ Networking/
│  │  │     ├─ Protocol/       transport-independent contracts
│  │  │     └─ Replication/    network actors and authoritative models
│  │  ├─ Client/
│  │  │  ├─ Networking/        connection and local presentation bridges
│  │  │  └─ UI/                client-only user interface
│  │  ├─ Server/
│  │  │  └─ Networking/        admission, sessions, spawning, authority
│  │  ├─ Characters/           reusable character simulation
│  │  ├─ Inventory/            reusable inventory domain
│  │  ├─ Quests/               reusable quest domain
│  │  └─ ...                   other reusable game domains
│  ├─ Editor/Networking/       project-specific bake/build automation
│  ├─ Tests/EditMode/Networking/
│  ├─ Content/Networking/      network prefabs and authored configuration
│  └─ Scenes/Prototype/        current vertical-slice world
├─ Documentation/Networking/
└─ Builds/
   ├─ LocalClient/
   └─ LocalServer/
```

## Dependency direction

```text
Protocol
   ↓
Shared replication and deterministic authority models
   ↓                         ↓
Client adapters          Server adapters
   ↓                         ↓
Client executable        Dedicated-server executable
```

Additional rules:

- Protocol never references gameplay, Netcode behaviours, client, server, or UI.
- Shared networking may reference Protocol and Netcode, but never Client, Server, gameplay presentation, or UI.
- Client and Server may both reference Shared and reusable gameplay domains.
- Client must never reference Server; Server must never reference Client or UI.
- Reusable gameplay must not call upward into networking adapters. Adapters translate network commands and snapshots at the boundary.
- Stable Unity GUIDs are preserved when source or assets move.

The project structure validator enforces assembly placement and these forbidden dependencies.

## Why gameplay is not all under Shared yet

The existing `UnityIsekaiGame.Gameplay` assembly still contains some prototype-local input and presentation coupling. Calling that entire folder “Shared” would make the tree misleading. Each domain should move into a Shared package only after local-player assumptions are removed and its mutation entry points can be invoked by server adapters.

The extraction order is:

1. Network protocol, replication, client, server, and UI physical roots — complete.
2. Server-authoritative inventory commands and snapshots — complete in Phase 5 Group 6.
3. Combat and ability command boundaries — complete in Phase 5 Group 7 for the Prototype Scene vertical slice.
4. Quest, party, and interaction command boundaries — complete in Phase 5 Group 8 for the Prototype Scene vertical slice.
5. Server-only persistence ownership and player-session restore — complete in Phase 5 Group 9.
6. Split reusable gameplay domains into embedded versioned packages.
7. Create thin `Projects/Client` and `Projects/Server` Unity projects consuming the same packages and content bundles.

After Group 9, two groups remain to reach the physical project split: Group 10 creates reusable versioned packages and explicit content boundaries, then Group 11 creates the separate Client and Server Unity projects that consume them.

## Target physical-project layout

After the mutation boundary is complete, the repository can become:

```text
UnityIsekaiGame/
├─ Projects/
│  ├─ Client/                 thin Unity presentation project
│  └─ Server/                 thin Unity dedicated-server project
├─ Packages/
│  ├─ com.thequantifier.isekai.protocol/
│  ├─ com.thequantifier.isekai.simulation/
│  ├─ com.thequantifier.isekai.networking/
│  └─ com.thequantifier.isekai.content/
├─ Documentation/
└─ Tools/
```

Both projects will reference the same packages by relative path. Shared code and schemas will not be copied. Client-only art/UI will not enter the server project, and server persistence/authority code will not enter the client project.

## Build outputs

- `Builds/LocalClient/UnityIsekaiClient.exe` is the player process.
- `Builds/LocalServer/UnityIsekaiServer.exe` is the dedicated authoritative process.

Build output is generated and ignored by Git. Source authority comes from the assembly roots above, not from generated executables or IDE project files.

## Group 5 verification

- Project structure validation: 8 passed, 0 failed.
- Full EditMode suite: 1,396 passed, 0 failed.
- Full PlayMode suite: 5 passed, 0 failed.
- Windows dedicated-server build: succeeded.
- Windows client build: succeeded.
- Separate-process loopback: connection approval, authoritative actor creation, server movement, stamina spending, and server recovery all observed.

## Group 8 verification

- Full EditMode suite: 1,414 passed, 0 failed.
- Full PlayMode suite: 5 passed, 0 failed.
- Windows dedicated-server and client builds: succeeded.
- Separate-process narrative smoke: authoritative interaction, quest-source browse, and party creation all observed.
