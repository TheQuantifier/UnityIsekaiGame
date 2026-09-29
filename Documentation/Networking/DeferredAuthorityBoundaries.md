# Deferred Authority Boundaries

This register keeps intentional temporary limits visible until their owning Phase 5 group implements and verifies them.

| Boundary | Current connected behavior | Required authoritative completion |
| --- | --- | --- |
| Physical item drop | Server removes the requested inventory quantity; no world pickup is spawned. | Atomically create a server-owned world item entity, replicate its item identity/state and transform, and roll back inventory removal if spawn fails. |
| World pickup | Connected client inventory guards reject local pickup mutation. | Server validates proximity/eligibility, transfers the exact world item identity into inventory, and despawns or reduces the authoritative pickup. |
| Loot and quest rewards | Connected replicas cannot grant items locally. | Server resolves reward definitions, capacity, identity creation, and all-or-nothing inventory/currency delivery. |
| Ammunition | Online ranged attacks that require ammunition are rejected. | Server atomically reserves/consumes ammunition with attack execution and refunds it if the authoritative shot cannot begin. |
| Crafting/disassembly | Connected replicas reject local inventory transformations. | Server owns recipes, inputs, catalysts, outputs, identity/composition/quality/durability changes, and rollback. |
| Projectile presentation | Spell collision/damage is server-simulated; clients receive outcomes and target snapshots without a replicated flight visual. | Replicate presentation-only projectile spawn/path/despawn events while retaining server collision and impact authority. |

The physical-drop requirement is not optional: replacing an online drop with permanent removal is only a temporary Group 6 boundary, not the final game behavior.
