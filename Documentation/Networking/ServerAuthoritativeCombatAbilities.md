# Server-Authoritative Combat and Abilities

## Group 7 objective

Phase 5 Group 7 moves connected-player attacks, spell casts, cooldowns, resource costs, projectile impacts, enemy combat state, and enemy damage behind the dedicated-server boundary. Offline play retains the existing local systems.

## Boundary

```text
Client input + selected authored spell ID
                 |
                 v
Sequenced combat command (action + normalized aim)
                 |
                 v
Server validates ownership, facing, equipment/ability, cooldown, cost, range, collision, target
                 |
                 v
Server simulates melee/projectile and mutates canonical Health/vitals
                 |
                 v
Global combatant snapshot + owner command result
                 |
                 v
Client presents read-only enemy/player replicas and feedback
```

The server derives damage, range, cost, cooldown, delivery, and effects from its own definition catalog. It resolves the player's current main-hand equipment from the authoritative inventory/equipment domain and resolves known spells from the server-configured player spell profile. A client cannot supply damage, cost, cooldown, target identity, hit result, or item state.

## Enemy authority

The server is the only process that runs prototype enemy movement and attacks during connected play. Each combat-capable scene enemy must have canonical Health and a stable `WorldEntityIdentity`. The global combat world state publishes entity ID, transform, Health, maximum Health, and defeat state. Connected clients disable their local enemy controllers and apply these snapshots as presentation replicas.

Enemy melee attacks recognize the network player's authoritative Health boundary and mutate `NetworkPlayerVitals` on the server. Local player Health remains a read-only projection through the existing vitals bridge.

## Projectile authority

Spell projectile paths and collision queries run only on the server. The initial Group 7 presentation reports cast and impact outcomes and replicates the resulting target state. Network-visible projectile VFX can be added later without moving collision or damage authority back to clients.

## Security and correctness rules

- Commands are owner-only and strictly sequenced, including unsigned wraparound.
- Aim vectors must be finite and normalized; horizontal aim must remain within the allowed angle of the server actor's facing.
- The server resolves equipment and known abilities from canonical state.
- Cooldowns and resource spending are server-owned and use unscaled server time.
- Failed resource checks roll back a newly reserved cooldown.
- Enemy snapshots reject duplicate identities, non-finite transforms, and invalid Health ranges.
- Connected client combat and enemy resource containers reject ordinary local mutation.
- Offline gameplay remains operational when no network actor is bound.

## Deliberate later boundaries

Ranged weapons that require ammunition are rejected online until the ammunition transaction group can atomically validate and consume server inventory. Replicated physical world drops and pickups, loot/reward grants, crafting transactions, durability/quality mutation, and network projectile presentation are tracked in `DeferredAuthorityBoundaries.md` and are not approximated with client-owned state.

## Verification

Verified on September 29, 2026:

- all 1,409 EditMode tests passed;
- all 5 PlayMode tests passed;
- the Windows dedicated-server and client builds completed successfully;
- a separate-process loopback smoke test connected a client, resolved an authoritative Arcane Bolt cast, spent mana from 100 to 80, reduced the target's Health from 65 to 47, and disconnected cleanly;
- the server smoke log contained no exceptions, combat errors, or pre-spawn `NetworkVariable` writes.
