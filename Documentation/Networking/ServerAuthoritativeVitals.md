# Server-Authoritative Vitals

## Goal

Phase 5 Group 4 makes player health, stamina, mana, regeneration, sprint cost, and the initial active/defeated lifecycle state server-owned. A connected client presents replicated values through the existing HUD and gameplay views, but it cannot mutate its local resource collection.

## Runtime flow

1. The dedicated server opens a validated player session and creates the registered network player actor.
2. `LocalDedicatedServer` takes the initial values and calculated maxima from the server-side Prototype player resource model. It never accepts those values from the connecting client.
3. `NetworkPlayerVitals` owns an `AuthoritativeVitalsModel` and publishes a server-write/everyone-read `NetworkVitalsState`.
4. `NetworkPlayerMovement` treats sprint as intent. The server checks lifecycle and stamina, spends the authoritative stamina cost, and chooses walk or sprint speed.
5. Server regeneration advances with unscaled server time, independent of client menus or client time scale.
6. `LocalPlayerVitalsBridge` binds only to the local owned actor and copies replicated health, stamina, mana, maxima, and defeat state into the existing local presentation model.
7. While connected, `CharacterResourceCollection` enters external-replica mode. Local damage, healing, spending, restoration, save restore, maximum reconciliation, and automatic resource ticks are rejected or suppressed.
8. Existing `PlayerHealth`, `PlayerStamina`, `PlayerMana`, and HUD subscribers continue receiving normal resource events from the replicated mirror.
9. Disconnecting removes replica authority and restores the standalone prototype resource path.

## Authority rules

- The client sends movement and sprint intent, never stamina values or sprint permission.
- Only server code can damage, heal, spend mana, restore resources, or revive the network actor.
- Reaching zero health changes the server lifecycle state to `Defeated` and blocks movement, jumping, sprinting, mana spending, healing, and automatic regeneration.
- Revive is an explicit server operation and restores all three vitals to their authoritative maxima.
- The local resource collection is a read-only compatibility/presentation mirror while connected.
- Dynamic maxima are initialized from the server scene's calculated resource model rather than trusting client-provided values.

## Relevant folders

```text
Assets/_Project/
├─ Runtime/
│  ├─ Shared/Networking/
│  │  ├─ Protocol/      connection payloads and versioned wire contracts
│  │  └─ Replication/   replicated actors, movement, vitals, and deterministic models
│  ├─ Server/Networking/ dedicated-server lifecycle, admission, sessions, and spawning
│  ├─ Client/Networking/ local connection, input bridges, and presentation mirroring
│  ├─ Characters/    shared character/resource/stat simulation used behind authority
│  ├─ Gameplay/      player-facing gameplay views and prototype adapters
│  ├─ UI/            client presentation only
│  └─ Input/         client input collection only
├─ Content/Networking/Prefabs/NetworkPlayerActor.prefab
└─ Scenes/Prototype/PrototypeScene.unity
```

The server and client are currently assemblies and separate executables in one Unity project. They are not duplicated source trees. This keeps shared schemas and deterministic models in one place while the assembly boundaries prevent presentation code from becoming the authority.

## Authored integration

Use `Tools > Unity Isekai Game > Networking > Bake Local Network Foundation` after changing player movement/resource tuning or the Prototype Scene player. The bake:

- adds `NetworkPlayerVitals` to the registered network actor prefab;
- copies authored sprint and regeneration tuning into its fallback configuration;
- wires the server to its server-side resource source; and
- wires `LocalPlayerVitalsBridge` to the local presentation resource collection.

The optional `--vitals-smoke-sprint` client flag submits sprinting movement after connection and logs both authoritative stamina spending and later server recovery. It is disabled during normal play.

## Group 4 verification

Verified on September 29, 2026 with Unity `6000.5.4f1`:

- Focused authoritative-vitals tests: 3 passed, 0 failed.
- Baked network foundation integration test: 1 passed, 0 failed.
- Full EditMode suite: 1,395 passed, 0 failed.
- Full PlayMode suite: 5 passed, 0 failed.
- Windows dedicated-server build: succeeded.
- Windows client build: succeeded.
- Separate-process loopback: connected, created the authoritative actor, and moved it on the server.
- Client replication: observed authoritative stamina spend from `160.00` to `159.90`, a low-water mark of `152.50`, and later server recovery to `152.90`.
- Compile/build output: no C# warnings or errors.

## Next boundary

The next gameplay-authority group should route combat, ability, item, and persistence commands through validated server request handlers. Those systems must call `NetworkPlayerVitals` server operations rather than re-enabling local resource mutation.
