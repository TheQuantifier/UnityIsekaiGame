# Project Structure Organization

Maintenance M1 reorganizes project-owned Unity assets under `Assets/_Project`.

## Top-Level Asset Ownership

- `Packages/com.thequantifier.isekai.simulation/Runtime`: reusable runtime code owned by the game project.
- `Packages/com.thequantifier.isekai.content/Content`: authored definitions intended to become permanent game content.
- `Assets/_Project/Prototype`: prototype-only authored content, prefabs, and test-lab scene support.
- `Assets/_Project/Presentation`: materials and other user-facing presentation assets.
- `Assets/_Project/Configuration`: input, rendering, and project-owned configuration assets.
- `Assets/_Project/Development`: runtime development tools such as the prototype Test Lab.
- `Assets/_Project/Editor`: editor-only setup, validation, and maintenance tools.
- `Assets/_Project/Tests`: automated test code and test-only fixtures.
- `Assets/_Project/Scenes`: Unity scenes, grouped by purpose.

`Assets/ThirdParty` and `Assets/StreamingAssets` are reserved for future use and should only be created when needed.

## Runtime Authority Roots

Runtime code is physically separated by execution authority:

- `Runtime/Shared`: contracts, deterministic models, and replicated state required by both executables. It must not reference Client, Server, UI, or local input code.
- `Runtime/Client`: connection clients, local input adapters, cameras, presentation bridges, and other code that may run only on a player's process.
- `Runtime/Server`: admission, sessions, authoritative command handling, spawning, persistence ownership, and world simulation adapters.
- Other folders directly under `Runtime` currently contain reusable game-domain code. Those systems move under `Shared` only after their APIs no longer depend on prototype player presentation.

Networking follows the enforced layout:

```text
Runtime/
├─ Shared/Networking/
│  ├─ Protocol/
│  └─ Replication/
├─ Client/
│  ├─ Networking/
│  └─ UI/
└─ Server/Networking/
```

Project-specific networking authoring lives in `Editor/Networking`, network tests live in `Tests/EditMode/Networking`, and architecture/history documents live in `Documentation/Networking`.

## Character and Actor Boundaries

- `Runtime/Characters` owns reusable character systems shared by players, NPCs, companions, enemies, summons, and future remote players.
- `Runtime/Characters/Runtime` owns the character coordinator/facade, readiness, snapshots, query access, revision signaling, and integrity checks.
- `Runtime/Characters/Stats`, `Resources`, `Skills`, `Traits`, `Capabilities`, `Requirements`, and `Progression` own their respective character subsystems.
- `Runtime/Actors/Player` owns local-player input/camera/bootstrap behavior and may depend on reusable character systems.
- `Runtime/Actors/People` owns persistent person identity and person-level associations.
- `Runtime/Actors/Beings` owns embodied actor concepts that are not necessarily people.
- `Runtime/Actors/WorldEntities` owns world entity identity, registration, and scene presence.

Dependency direction should flow from player-specific systems toward reusable character and core systems, not the other way around.

## Content Placement

Reusable character definitions such as attributes, calculated stats, resources, roles, social statuses, skills, and traits live under `Packages/com.thequantifier.isekai.content/Content`.

Prototype-only gameplay definitions such as the current health potion, prototype abilities, prototype contracts, prototype quests, and the `PrototypeDefinitionCatalog` live under `Assets/_Project/Prototype/Content`.

Stable definition IDs are not derived from folder paths. Moving assets must preserve `.meta` GUIDs and must not change stable IDs.

## Assembly Organization

M1.1 splits project-owned code into explicit assemblies:

- `UnityIsekaiGame.GameData`: stable definition interfaces, catalogs, registry, validation, and persistence definition primitives.
- `UnityIsekaiGame.Gameplay`: production runtime gameplay systems. It may reference `GameData`, but not UI, Development, Editor, or Tests.
- `UnityIsekaiGame.UI`: client-only production runtime UI under `Runtime/Client/UI`. It may reference `GameData` and `Gameplay`.
- `UnityIsekaiGame.Development`: Editor/development-build tools such as the Prototype Test Lab. It may reference runtime/UI contracts, but production runtime assemblies must not reference it.
- `UnityIsekaiGame.Editor`: Editor-only validation, setup, and batch tooling.
- `UnityIsekaiGame.EditModeTests`: Editor-only test assembly.
- `UnityIsekaiGame.Networking.Protocol`: transport-independent payloads, launch options, endpoint validation, and session records under `Runtime/Shared`.
- `UnityIsekaiGame.Networking.Shared`: Netcode replication and deterministic authoritative models under `Runtime/Shared`.
- `UnityIsekaiGame.Networking.Client`: local connection and presentation/input bridges under `Runtime/Client`.
- `UnityIsekaiGame.Networking.Server`: server lifecycle, admission, sessions, and spawning under `Runtime/Server`.

Dependency direction is intentionally one-way:

`GameData -> Gameplay -> UI -> Development -> Editor/Tests` is not a chain of upward references; it describes allowed higher layers depending on lower layers. Lower layers must not depend upward.

The permanent menu extension point is owned by `UnityIsekaiGame.UI` and is intentionally small and menu-specific. Development code implements that contract with the Test Lab adapter. UI code must not mention `PrototypeTestLabService`, `PrototypeTestLabView`, Development namespaces, or editor-only types.

## Production Configuration

The project must support a production configuration where Development, Test Lab, Tests, Editor-only tooling, and Prototype-only content are excluded without production runtime code changes.

Current rules:

- Production runtime code lives in `GameData`, `Gameplay`, `UI`, and the four networking assemblies.
- Production runtime assemblies must not reference `UnityIsekaiGame.Development`, `UnityIsekaiGame.Editor`, or `UnityIsekaiGame.EditModeTests`.
- Production prefabs must not contain Development components.
- Production scenes must not contain Test Lab components.
- Production ScriptableObject assets must not reference prototype-only assets.
- `Development` may be excluded from non-development player builds. The permanent menu still compiles and runs without registered extensions.
- `Editor` and `EditModeTests` are restricted to the Editor platform.
- Build scenes must be categorized by path as production or development/prototype.
- Prototype-only authored content lives under `Assets/_Project/Prototype` and must not be required by production runtime code through hardcoded paths.
- Prototype content may depend on production content. Production content must not depend on prototype content.
- Production-required definitions, scenes, prefabs, and configuration assets should live outside `Assets/_Project/Prototype`.
- Prototype content can be removed from a production build profile only after production content/catalogs have been supplied for any feature that currently uses prototype definitions.

## Validation

Use `Tools > Project Maintenance > Validate Project Structure` after project import. The validator checks top-level folder placement, missing and orphan `.meta` files, duplicate GUIDs, obsolete hardcoded paths, missing script references, asmdef name duplication, required assemblies, forbidden assembly references, dependency cycles, production-runtime imports of Development/UI/Editor where forbidden, production prefab/scene/content references across the Development/Test Lab/Prototype boundary, build-scene categorization, and known moved canonical assets.
