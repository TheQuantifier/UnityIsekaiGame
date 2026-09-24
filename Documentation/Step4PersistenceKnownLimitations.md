# Step 4 Persistence Known Limitations

This is the authoritative Step 4 deferred persistence register.

| Limitation | Current Behavior | Risk | Intended Future Milestone | Extension Support |
| --- | --- | --- | --- | --- |
| Complete shared-world coverage | Location/travel, economy, organizations, politics, social, quest, dialogue, and narrative runtimes use a separate world checkpoint. | Spawned actors, pickups, doors, and containers still reset unless covered by a domain participant. | Add focused world participants as those runtimes become authoritative. | Player and world services, directories, readiness, and audits are separate. |
| Collected pickup state | Player inventory persists; world pickup removal does not. | Reset worlds can expose already-collected local pickups. | Region/world entity state participant. | World entity IDs exist. |
| Enemy position/health/death | Not persisted. | Enemies reset with scene/runtime setup. | Actor/world-state persistence. | Being definitions and world IDs exist. |
| NPC runtime state and schedules | Not persisted. | NPC conversations/schedules cannot resume dynamic runtime state. | People/NPC model expansion. | Person definitions and world IDs exist. |
| Doors | Not persisted. | Open/locked state resets. | Region interactable persistence. | World entity references can identify doors later. |
| Containers | Not persisted. | Contents and looted state reset. | Region/container participant. | Inventory DTO patterns can be reused. |
| Spawned world-entity manifests | Test Lab proof only. | Runtime spawned loot is not durable across real saves. | Region spawned-entity manifest. | Runtime world entity IDs already compose with world ID. |
| Settlement and economy state | Not implemented. | No durable economic simulation. | Economy/settlement milestone. | SharedWorld scope is available. |
| Faction state | Static definitions only. | No durable reputation or war-state changes. | Faction runtime milestone. | Faction IDs and SharedWorld/Account scopes are available. |
| World-event state | Not implemented. | Events cannot persist or progress offline. | World simulation milestone. | SharedWorld scope is available. |
| Multiplayer server persistence | Not implemented. | Local saves are not authoritative or secure. | Server persistence milestone. | Ownership model avoids client-world ownership assumptions. |
| Cloud saves | Not implemented. | Saves are local only. | Account/platform milestone. | Save envelope has account/world/player IDs. |
| Cross-scene restore | Rejected clearly. | Player can only restore same scene in Step 4. | Scene loading service milestone. | Scene keys and spawn fallback already exist. |
| Cross-scene rollback | Not complete. | Failed cross-scene restore is not supported. | Scene loading service milestone. | Rollback guard exists for same-scene participant rollback. |
| Offline world progression | Not implemented. | Timed statuses do not elapse offline; world does not simulate while absent. | Server simulation/offline progression milestone. | Status persistence policies can be extended. |
| Release-grade migrations | Minimal migration extension point only. | Development saves may be invalidated by model changes. | Alpha/beta save compatibility milestone. | Independent participant schemas support future migrations. |
| Crash guarantees | Atomic local write and backup only. | Does not guarantee recovery from all OS/storage failures. | Hardened platform storage milestone. | Temp validation and backups exist. |
| Anti-tamper security | Checksum detects accidental corruption only. | Local files are user-editable. | Server authority/security milestone. | Server-owned persistence boundary documented. |
| Final title-screen save selection | Prototype Tab menu only. | Save UX is not final game flow. | Main menu/title flow milestone. | Slot descriptors and metadata already exist. |
| Generated development artifacts | Runtime saves live under persistent data path. | Manual tests can leave local files outside repo. | Ongoing cleanup discipline. | Repo scans should keep generated files unstaged. |
