# Server-Authoritative Movement

## Goal

Phase 5 Group 3 moves the Prototype Scene's player locomotion across the client/server boundary. The client samples movement intent and camera yaw, but it cannot write position, velocity, grounding, or jump state. The dedicated server validates the input, performs collision-aware movement, and replicates the resulting transform.

## Runtime flow

1. Group 2 creates a server-owned `NetworkPlayerActor` for the approved player session.
2. `LocalPlayerMovementBridge` binds only to the local client's owned actor.
3. While connected, the bridge disables `FirstPersonCharacterMotor`, leaving input, camera, HUD, and presentation on the client.
4. The client sends sequenced continuous movement intent through an owner-only, unreliable RPC; jump edges use a separate reliable owner-only request.
5. The server rejects replayed, out-of-order, or non-finite commands and clamps movement magnitude.
6. `NetworkPlayerMovement` performs acceleration, deceleration, sprinting, gravity, jumping, and `CharacterController` collision on the server.
7. the server-authoritative `NetworkTransform` replicates position and yaw.
8. The client presentation root follows the replicated network actor. It cannot directly move the actor.
9. If input stops arriving, the server clears movement after a short timeout. Disconnecting still despawns the actor through the Group 2 lifecycle.

The legacy prototype controller is disabled while the dedicated server is hosting so it cannot collide with the network actor at the shared spawn. It is restored if the server stops in the Editor. On the client, the old motor is restored with cleared transient velocity after disconnect.

## Authored assets

- Player actor prefab: `Packages/com.thequantifier.isekai.content/Content/Networking/Prefabs/NetworkPlayerActor.prefab`
- Client bridge: `Packages/com.thequantifier.isekai.client/Runtime/Networking/LocalPlayerMovementBridge.cs`
- Authoritative movement: `Packages/com.thequantifier.isekai.networking/Runtime/Shared/Networking/Replication/NetworkPlayerMovement.cs`
- Input contract and validation: `Packages/com.thequantifier.isekai.networking/Runtime/Shared/Networking/Replication/NetworkMovementInput.cs`
- Prototype Scene network host: `Assets/_Project/Scenes/Prototype/PrototypeScene.unity`

Run `Tools > Unity Isekai Game > Networking > Bake Local Network Foundation` after changing the player prefab or Prototype Scene player references. The bake adds and configures the movement components, registers the prefab, captures the authored spawn, and wires the presentation bridge.

## Authority and safety rules

- RPC invocation requires ownership of the spawned player object.
- The server also verifies the RPC sender against `OwnerClientId`.
- Sequences must advance, including across unsigned wraparound.
- NaN and infinity are rejected.
- Movement magnitude is clamped to one and yaw is normalized.
- Sprint speed, acceleration, gravity, jump height, collision, and the final transform are server-owned.
- A stale-input timeout prevents a disconnected or stalled client from continuing to move.
- Menus still block local gameplay input without pausing the server.

Client prediction and reconciliation are deliberately deferred until measured latency justifies them. Loopback play uses interpolated authoritative transforms, avoiding a second source of movement truth during the initial vertical slice.

## Repeatable smoke test

The client-only `--movement-smoke-forward` flag submits forward input for 1.5 seconds after the local actor becomes ready. It is intended for automated local build verification:

```powershell
.\UnityIsekaiClient.exe -batchmode -nographics --local-client --server-address 127.0.0.1 --server-port 7777 --player-id movement-test --movement-smoke-forward -logFile client.log
```

The server log must contain `Server moved actor`; the client log must contain `Client observed authoritative movement`. The normal game never enables this input unless the explicit flag is present.

## Group 3 verification

Verified on September 29, 2026 with Unity `6000.5.4f1`:

- Focused movement tests: 4 passed, 0 failed.
- Full EditMode suite: 1,392 passed, 0 failed.
- Full PlayMode suite: 5 passed, 0 failed.
- Windows dedicated-server build: succeeded.
- Windows client build: succeeded.
- Separate-process loopback: connected and resolved the Group 2 actor identity.
- Server movement: accepted sequenced client intent and moved the actor using the server controller.
- Client replication: observed 3.72 meters of authoritative movement.
- Compile output: no C# warnings or errors.

## Next boundary

Group 4 should make health, stamina, mana, regeneration, sprint cost, and lifecycle state server-owned. In particular, sprint permission and resource drain must move from the local prototype stamina component into an authoritative character-state adapter; the Group 3 sprint flag is currently movement intent, not permission to spend stamina.
