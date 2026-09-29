# Versioned Package Boundaries

Phase 5 Groups 10 and 11 promote the reusable vertical-slice code, adapters, and shared data into local Unity Package Manager packages. Each Isekai package currently uses version `0.1.0`; releases must change package versions deliberately and keep local inter-package dependency versions aligned.

## Ownership matrix

| Package | Owns | Must not own |
| --- | --- | --- |
| Protocol | transport-neutral DTOs, command results, session and save wire contracts | MonoBehaviours, Netcode, gameplay implementations, UI |
| Simulation | definitions, runtime domains, deterministic gameplay, persistence participants | client UI, transport adapters, server admission |
| Networking | shared replication used by both processes | client adapters, server adapters, authored definitions |
| Client | connection lifecycle, input/presentation bridges, UI | server authority, server persistence |
| Server | admission, commands, authoritative adapters, persistence | client input, cameras, UI |
| Content | shared definitions, configuration, generated records, network prefabs | C# source, client-only world art |
| Project Tools | editor-only extraction and repository tooling | runtime behavior |

## Versioning rules

- Use semantic versions in every `package.json`.
- Keep dependent package constraints exact while the repository is developed and released as one coordinated product.
- Update `CHANGELOG.md` in every package whose public API or serialized content changes.
- Preserve `.meta` files when moving Unity assets between a project and a package.
- A breaking protocol or serialized-save change requires a coordinated major-version decision before shipping.
- Generated `.csproj` files are not package boundaries and must not be hand-edited.

## Content split

The Content package contains assets required by both executables, including authoritative definitions and network prefabs. `Projects/Client/Assets/_Project/Prototype` and the Prototype Scene remain client-only inputs. `Projects/Server` contains a stripped headless scene and does not import the client visual library.

## Validation

`Tools > Project Maintenance > Validate Project Structure` in the Client project checks package existence, coordinated versions, required dependencies, code/content separation, assembly placement, dependency direction, GUID uniqueness, and the absence of legacy runtime/content roots. Client and Server EditMode suites independently enforce their project manifests and high-risk execution boundaries.
