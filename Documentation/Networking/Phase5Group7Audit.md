# Phase 5 Group 7 Audit

Group 7 began from commit `2f8d67f` on `phase-5/group-7-authoritative-combat-abilities`.

The audit found that offline combat was internally coherent but crossed every multiplayer trust boundary: the client consumed attack/cast input, selected equipment and abilities, spent resources, advanced cooldowns and projectiles, raycast targets, and mutated enemy Health directly. Enemies also simulated independently in every process and targeted the scene prototype player rather than the server-owned network actor.

Group 7 preserves the offline path and introduces a separate connected-play boundary:

- sequenced owner-only primary-attack and ability-cast commands;
- normalized finite aim validation and authoritative facing checks;
- server-side equipment, known-spell, cooldown, resource, range, collision, and target validation;
- server-owned melee resolution and spell projectile simulation;
- server-owned enemy movement/attack simulation against network actors;
- globally replicated enemy transforms, Health, maximum Health, and defeat state;
- read-only client enemy Health replicas with local enemy AI disabled while connected;
- connected-client guards that prevent the legacy melee and spell systems from consuming input or mutating combat state;
- bounded command results for HUD feedback;
- protocol, replica-lock, resource-cost, and scene/prefab authoring tests.

The network player actor intentionally remains a compact replicated identity/movement/vitals/inventory/combat shell. Static definitions and the existing inventory/equipment ownership model are resolved only on the server; the client sends neither damage values nor resource costs.

The end-to-end smoke pass also caught inherited identity replication being assigned before the player `NetworkObject` spawned. Identity is now staged locally during session construction and published from `OnNetworkSpawn`, removing the Netcode initialization warnings while preserving pre-spawn validation access.
