# Phase 5 Group 11 Physical Split Audit

## Result

The playable client and authoritative dedicated server are now separate Unity projects under `Projects/Client` and `Projects/Server`. They share versioned repository packages and cannot compile the opposite process's adapter assembly through their project manifests.

## Client

- Owns the Prototype Scene and all player-facing art, input, camera, UI, editor tooling, and regression tests.
- Installs Client, Shared Networking, Protocol, Simulation, Content, Project Tools, and Scene Zone Tool packages.
- Does not install the Server package.
- Prototype Scene contains client networking bridges and no `LocalDedicatedServer` component.

## Server

- Owns a small headless scene and dedicated build automation.
- Installs Server, Shared Networking, Protocol, Simulation, Content, and Scene Zone Tool packages.
- Does not install the Client package or client UI assembly.
- Generated scene retains server bootstrap, Netcode prefabs, authoritative simulation, and required configuration.
- Generated scene contains no Camera, Renderer, client/UI assembly component, or missing script.

## Shared source of truth

- Protocol: `Packages/com.thequantifier.isekai.protocol`
- Simulation: `Packages/com.thequantifier.isekai.simulation`
- Shared replication: `Packages/com.thequantifier.isekai.networking`
- Client adapters/UI: `Packages/com.thequantifier.isekai.client`
- Server adapters/persistence: `Packages/com.thequantifier.isekai.server`
- Shared authored data: `Packages/com.thequantifier.isekai.content`

No gameplay implementation is duplicated between the two Unity projects.

## Verification

Verified with Unity `6000.5.4f1` on September 29, 2026:

- Client fresh import and script compilation: passed.
- Client EditMode: 1,416 passed, 0 failed.
- Client PlayMode: 5 passed, 0 failed.
- Client Windows build: passed.
- Server fresh import and script compilation: passed.
- Server EditMode: 6 passed, 0 failed.
- Windows dedicated-server build: passed.
- Separate-process loopback: server listened on `127.0.0.1:7798`, approved `group11-verification`, and the client connected.
