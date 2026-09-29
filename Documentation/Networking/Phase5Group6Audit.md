# Phase 5 Group 6 Audit

Group 6 began from commit `e76d071` on `phase-5/group-6-authoritative-inventory-equipment`.

The audit confirmed that the existing inventory/equipment ownership model is sound: inventory owns item instances and equipment contains references only. The networking work will preserve that design rather than reintroducing a second equipment container.

The completed implementation adds:

- explicit command and failure enums;
- protocol limits for slots, quantities, equipment slots, IDs, and result messages;
- replay-safe command sequence validation;
- transport-safe inventory and equipment snapshot records;
- complete snapshot integrity validation;
- a server-owned inventory/equipment domain instance for every player session;
- owner-only reliable commands for use, equip, unequip, and drop quantity;
- server-side definition/effect resolution and validation;
- complete revision snapshots plus owner-only command results;
- end-of-network-frame, catalog-validated replica application on the client;
- hard mutation lockout on connected client inventory/equipment containers;
- inventory-menu routing through the authority bridge;
- in-memory authoritative state retention across disconnect/reconnect;
- automated tests for replay, wraparound, slot bounds, quantities, duplicate identities, equipment ownership, message limits, and replica mutation guards;
- a two-client separate-process smoke test covering drop, equip, unequip, use, disconnect, and reconnect restore.

Offline play retains the existing domain APIs. While connected, those APIs reject local writes unless the call is the dedicated replica application path. Cross-domain systems such as crafting, rewards, ammunition, and pickups therefore cannot silently mutate the client; their server commands are added with their own Phase 5 authority groups.
