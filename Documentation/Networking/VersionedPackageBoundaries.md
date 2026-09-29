# Versioned Package Boundaries

Phase 5 Group 10 promotes the reusable vertical-slice code and shared data into four embedded Unity Package Manager packages. Each package currently uses version `0.1.0`; releases must change package versions deliberately and keep local inter-package dependency versions aligned.

## Ownership matrix

| Package | Owns | Must not own |
| --- | --- | --- |
| Protocol | transport-neutral DTOs, command results, session and save wire contracts | MonoBehaviours, Netcode, gameplay implementations, UI |
| Simulation | definitions, runtime domains, deterministic gameplay, persistence participants | client UI, transport adapters, server admission |
| Networking | shared replication plus Client and Server adapters | authored game definitions, large visual content |
| Content | shared definitions, configuration, generated records, network prefabs | C# source, client-only world art |

## Versioning rules

- Use semantic versions in every `package.json`.
- Keep dependent package constraints exact while the repository is developed and released as one coordinated product.
- Update `CHANGELOG.md` in every package whose public API or serialized content changes.
- Preserve `.meta` files when moving Unity assets between a project and a package.
- A breaking protocol or serialized-save change requires a coordinated major-version decision before shipping.
- Generated `.csproj` files are not package boundaries and must not be hand-edited.

## Content split

The Content package contains assets required by both executables, including authoritative definitions and network prefabs. The `Assets/_Project/Prototype` visual library and Prototype Scene remain client-project inputs. Group 11 will give the server a small headless scene/configuration set rather than importing that visual library.

## Validation

`Tools > Project Maintenance > Validate Project Structure` checks package existence, coordinated version, required dependencies, code/content separation, assembly placement, dependency direction, GUID uniqueness, and the absence of legacy runtime/content roots. EditMode tests independently enforce the same high-risk contracts.
