# Deferred Authority Boundaries

This register keeps intentional temporary limits visible until an authority group implements and verifies them.

| Boundary | Current connected behavior | Required authoritative completion |
| --- | --- | --- |
| Loot and quest rewards | Connected replicas cannot grant items locally. | Server resolves reward definitions, capacity, identity creation, and all-or-nothing inventory/currency delivery. |
| Ammunition | Online ranged attacks that require ammunition are rejected. | Server atomically reserves/consumes ammunition with attack execution and refunds it if the authoritative shot cannot begin. |
| Crafting/disassembly | Connected replicas reject local inventory transformations. | Server owns recipes, inputs, catalysts, outputs, identity/composition/quality/durability changes, and rollback. |
| Projectile presentation | Spell collision/damage is server-simulated; clients receive outcomes and target snapshots without a replicated flight visual. | Replicate presentation-only projectile spawn/path/despawn events while retaining server collision and impact authority. |

## Completed boundaries

| Boundary | Completion |
| --- | --- |
| Physical item drop | Phase 6 Group 1 atomically spawns a server-owned replicated pickup before committing inventory removal, preserves full-stack identity, creates a unique split identity for partial stacks, and rolls back both sides on failure. |
| World pickup | Phase 6 Group 1 accepts non-owner pickup requests, validates the requesting session and server actor distance, transfers the exact replicated identity/storage mode into authoritative inventory, and despawns only after the transfer commits. |
