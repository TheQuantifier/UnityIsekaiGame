# Project Structure Organization

The repository is a Unity monorepo. Phase 5 Group 11 physically separates the playable Client and authoritative Server projects while retaining one shared package source of truth.

```text
UnityIsekaiGame/
|-- Projects/
|   |-- Client/                    playable scene, presentation, tests, settings
|   `-- Server/                    headless scene, server tests, settings
|-- Packages/                      shared code, adapters, data, editor tooling
|-- Documentation/
|-- Tools/                         repository-level automation
`-- Builds/                        generated and ignored
```

Open `Projects/Client` and `Projects/Server` as separate Unity Hub projects. The repository root is not a Unity project.

## Project ownership

### Client

- `Projects/Client/Assets/_Project/Prototype`: prototype-only visual content, prefabs, and Test Lab support.
- `Projects/Client/Assets/_Project/Presentation`: client-facing presentation assets.
- `Projects/Client/Assets/_Project/Configuration`: client input and rendering configuration.
- `Projects/Client/Assets/_Project/Development`: development-build tools.
- `Projects/Client/Assets/_Project/Editor`: client setup, validation, extraction, and build automation.
- `Projects/Client/Assets/_Project/Tests`: the full gameplay and client regression suite.
- `Projects/Client/Assets/_Project/Scenes`: client scenes grouped by purpose.
- `Projects/Client/Assets/ThirdParty`: client-only imported art and presentation assets.

### Server

- `Projects/Server/Assets/Scenes/ServerPrototypeScene.unity`: stripped headless vertical-slice scene.
- `Projects/Server/Assets/Editor`: server build automation.
- `Projects/Server/Assets/Tests`: server persistence and boundary tests.
- `Projects/Server/Assets/DefaultNetworkPrefabs.asset`: explicit Netcode prefab registration.

The Server project must not acquire client art, cameras, renderers, input bridges, UI, or development tools.

## Package ownership

```text
Packages/
|-- com.thequantifier.isekai.protocol/       transport-neutral contracts
|-- com.thequantifier.isekai.simulation/     reusable game domains and state
|-- com.thequantifier.isekai.networking/     shared Netcode replication
|-- com.thequantifier.isekai.client/         connection, input/presentation bridges, UI
|-- com.thequantifier.isekai.server/         admission, authority, persistence, bootstrap
|-- com.thequantifier.isekai.content/        shared authored definitions and network prefabs
|-- com.thequantifier.isekai.project-tools/  editor-only monorepo tooling
`-- com.thequantifier.scene-zone-tool/        shared zone runtime and authoring tool
```

The Client manifest includes the Client package and excludes the Server package. The Server manifest includes the Server package and excludes the Client package. Both reference shared packages with repository-relative local UPM paths.

## Runtime authority and dependencies

- Protocol never depends on simulation, Netcode behavior, client, server, or UI.
- Simulation owns definitions and deterministic gameplay domains. It never depends on client, server, shared replication, protocol, or UI.
- Shared Networking owns cross-process replication and depends downward on Protocol and Simulation.
- Client owns player-process adapters and UI. It cannot reference Server.
- Server owns authoritative process adapters and persistence. It cannot reference Client or UI.
- Content owns serialized assets required by both executables and contains no C# source.

Lower-level assemblies must not reference higher presentation, development, editor, or test layers. Client-specific systems may depend on reusable simulation; reusable simulation may not depend on local-player systems.

## Content placement

Reusable definitions such as attributes, calculated stats, resources, roles, social statuses, skills, traits, and network prefabs live in `Packages/com.thequantifier.isekai.content/Content`.

Prototype-only gameplay definitions such as the current prototype items, abilities, contracts, quests, and `PrototypeDefinitionCatalog` live in `Packages/com.thequantifier.isekai.content/Content/Prototype`.

Client-only world art and temporary scene presentation remain under `Projects/Client/Assets/_Project/Prototype`. Stable definition IDs are not derived from paths. Every move must preserve the asset's `.meta` GUID and stable ID.

## Assembly organization

- `UnityIsekaiGame.GameData`: definition interfaces, catalogs, registry, validation, and persistence primitives.
- `UnityIsekaiGame.Gameplay`: reusable runtime gameplay systems.
- `UnityIsekaiGame.Networking.Protocol`: transport-independent wire contracts.
- `UnityIsekaiGame.Networking.Shared`: shared Netcode replication.
- `UnityIsekaiGame.Networking.Client`: client connection and presentation/input bridges.
- `UnityIsekaiGame.Networking.Server`: server lifecycle, sessions, commands, and spawning.
- `UnityIsekaiGame.UI`: client-only production UI.
- `UnityIsekaiGame.Development`: editor/development-build Test Lab support.
- `UnityIsekaiGame.Editor`: client Editor-only validation and setup.
- `UnityIsekaiGame.EditModeTests`: client Editor-only regression tests.

## Production configuration

- Production prefabs must not contain Development components.
- Production scenes must not contain Test Lab components.
- Production ScriptableObject assets must not depend on prototype-only definitions.
- Development and Tests can be excluded from release clients without production code changes.
- Editor and EditMode test assemblies are restricted to the Editor platform.
- Build scenes must be categorized as production or development/prototype.
- Prototype content may depend on production content; production content must not depend on prototype content.

## Validation

In the Client project, run `Tools > Project Maintenance > Validate Project Structure`. It checks allowed asset roots, missing and orphaned meta files, duplicate GUIDs, package names and versions, code/content separation, obsolete paths, missing scripts, assembly placement and cycles, forbidden dependencies, production/prototype boundaries, build-scene categorization, and known canonical assets.

The Client EditMode suite independently checks the repository package and manifest boundaries. The Server EditMode suite verifies that only server adapters are registered and that the headless scene contains authority without client or presentation components.
