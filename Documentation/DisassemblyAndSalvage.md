# Disassembly and Salvage

`DisassemblyRuntime` is the single owner of item-recovery operation history and natural-decomposition schedules, with deliberately separate intentional, environmental, and collection modes.

## Disassembler

Disassembly is a controlled workstation operation on a crafted inventory item. It reads the item's recorded component composition, quality, rarity, level, and current item/component durability. `skill.disassembly` improves expected efficiency and component-return chances. Higher item level, rarity, and quality help preserve recoverable structure, while rare components are individually harder to recover.

Durability is a physical ceiling, not merely another skill modifier. Each damaged component has a lower return chance and a condition-based cap on returned quantity. Even an AAA Disassembler cannot return more material than remains. A successful operation removes the source item, creates inventory identities for recovered resources, closes its durability record, and marks its lifecycle `Disassembled` inside one rollback boundary.

## Salvager

Salvaging is field collection of loose material or explicitly marked broken-piece pickups. It does not break down owned equipment and uses `skill.salvaging`, independent of Disassembly.

The skill grade is the Salvager effectiveness rank:

| Skill grade | Double-yield chance |
|---|---:|
| Unlearned | 0% |
| F | 5.0% |
| E | 11.4% |
| D | 17.9% |
| C | 24.3% |
| B | 30.7% |
| A | 37.1% |
| AA | 43.6% |
| AAA (Master) | 50.0% |

The roll is deterministic per world pickup source. On success, the inventory receives twice the physical quantity that can actually be collected; generated bonus material never increases the quantity remaining on the ground. Inventory capacity can limit the bonus. Raw material pickups qualify automatically, and future debris prefabs can opt in explicitly.

## Natural decomposition of dropped items

Tracked world-dropped items with recoverable component composition use the authoritative world/playtime clock and the catalog-authored `durability-policy.standard` definition:

| Current durability | Decay behavior | Default delay |
|---|---|---:|
| Over 10% | Slow | 3,600 seconds |
| Over 5% through 10% | Fast | 300 seconds |
| 5% or less | Immediate | 0 seconds |

The policy owns the thresholds, durations, break-chance curve, and whether broken world items continue decaying. `PrototypePersistenceServiceBehaviour` contains no duplicate scene-level decay tuning. Crossing into a worse condition band can only preserve or shorten an existing deadline; repairing into a better band starts the longer timer from the current world time. Picking the item back up cancels its schedule. Player-dropped tracked items and individually tracked composed enemy-loot drops use this path. Schedules persist in shared-world item-recovery save data, so a server-authoritative clock can continue advancing them independently of the player who dropped the item.

Items make one persisted break roll at every whole percentage from 10% through 5%, using the chances authored in that policy. A dropped item that succeeds on any roll—including the final 5% roll—remains a dropped broken item and continues on the fast environmental-decomposition timer. If all six checks fail, reaching 5% forces immediate natural decomposition. The recovered world pieces are Salvager-eligible.

When the deadline is reached, the existing component recovery calculation runs with `actor.nature`, no character skill bonus, and the item's current level, rarity, quality, overall durability, component durability, and component rarity. The original item becomes `Disassembled`. Any successfully recovered resources appear as loose Salvager-eligible pickups; the Salvaging skill bonus is rolled when a character collects those parts. Raw material piles do not recursively decompose.

The prototype HUD reports entry into the critical durability range, a successful break and its percentage, failure of the final check and forced decomposition, and whether a newly dropped item is on the slow or fast decay schedule.

## Shared persistence and professions

All modes persist through the shared-world `world.item-recovery` participant and use one operation record format tagged as `Disassemble`, `NaturalDecomposition`, or `Salvage`. The profession catalog contains distinct `profession.disassembler` and `profession.salvager` roles and distinct professional activity definitions, so their experience evidence cannot be confused. Nature does not earn professional experience. The current gameplay multiplier is driven by the Salvaging skill grade; formal profession rank ladders can later govern titles, licensing, or job access without duplicating the yield formula.

Forced decomposition is transactional. Equipped outputs become world salvage, player-inventory outputs remain in player inventory, and container outputs remain in the same container. If a player inventory cannot hold every recovered identity, the entire operation rolls back with the source item and pending-decomposition state intact; it can be retried after space is available.
