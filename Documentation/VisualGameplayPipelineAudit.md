# Visual Gameplay Pipeline Audit

## Scope

This audit covers the complete path from authored content to the rendered client and the headless authoritative server for the Prototype Scene vertical slice. It includes world geometry and collision, materials and render settings, animation references, runtime UI, authoritative replication, persistence restore, scene extraction, client/server builds, and local multiplayer lifecycle.

## Pipeline map

1. **Authored definitions and content** live under `Packages/com.thequantifier.isekai.content` and the simulation packages. Definition catalogs connect items, equipment, spells, quests, social records, and scene bindings to stable IDs.
2. **Client scene assembly** uses `Projects/Client/Assets/_Project/Scenes/Prototype/PrototypeScene.unity`, shared content prefabs, URP assets, runtime-generated themed UI, and client authority bridges.
3. **Server scene extraction** is performed by `DedicatedServerSceneExtraction`. It synchronizes authoritative scene state from the client scene, redirects collision geometry to server-owned meshes, keeps terrain/tree collision, removes rendering/UI/audio/editor-only behavior, and stabilizes the prototype seed identity.
4. **Authoritative runtime** is hosted by `LocalDedicatedServer`. Movement, vitals, combat, inventory/equipment, narrative state, pickups, persistence, and player sessions mutate on the server.
5. **Replication** uses Netcode player actors, combat-world state, and world-item pickup objects. Client bridges submit commands and translate snapshots/results into local presentation without mutating server state directly.
6. **Client presentation** renders the world through URP, follows authoritative movement, creates spell projectile presentation, mirrors pickups, and binds replicated character/inventory/narrative state to the UI.
7. **Responsive UI** uses the shared brown/gold theme, runtime canvas scaling, safe-area-aware layout, local scrolling regions, and a top-sorted equipment-comparison hover canvas.
8. **Persistence** stores player profiles separately from the durable world checkpoint. The checkpoint envelope is inspected before load so current dynamic player identities can be registered without bypassing checksum or participant validation.

## Root causes found and corrected

- The first server split replaced authored collision with broad fallback boxes. Exact mesh colliders, terrain data, and colliding tree prototypes are now extracted into server-owned assets and validated in the server scene.
- Server terrain/tree collision marker materials referenced a render shader that is inappropriate in a headless build. They now use dedicated server-safe collision assets.
- Several stale URP/readme/probe resources and missing household script references survived the project split. Stale assets were removed or repaired and household definitions now have a valid runtime type.
- A stray scene container retained render geometry. The visual validator now detects renderers and mesh filters that escape the intended presentation hierarchy.
- Animator lookup assumed a fixed child location. Runtime binding now searches the complete child hierarchy and the validator checks controllers and clips.
- Low-resolution canvases retained a 1920x1080 reference, producing tiny text and compressed panels. Reference resolution now adapts below 1600 pixels while retaining the desktop design scale; themed body, muted, feedback, character, and status text enforce readable minimum sizes.
- The equipment comparison was rebuilt during inventory refresh and could disappear or render behind slots. Hover state now survives authoritative refresh, and the tooltip uses a nested high-order canvas.
- The visual capture path selected empty slots before authoritative inventory replication completed. It now waits for populated replicated inventory and captures real item details and an equippable comparison.
- Replicated pickup smoke selected an arbitrary matching scene pickup. It now selects the nearest matching pickup, verifying the exact dropped world instance.
- Definition-stack pickups carried their temporary world-instance IDs back into inventory, so individually dropped materials could no longer rejoin their original stacks. Collection now merges definition-stack items by definition and quantity first, retaining the world ID only when a new inventory stack is actually required; stateful/equippable items still preserve exact identity.
- Replicated pickups replaced every authored item model with the brown network collision capsule. Scene-authored pickups now remain as presentation-only models bound to their matching authoritative object, dynamic drops create renderer-only copies of the item pickup prefab, and the generic capsule is hidden whenever an authored presentation is available. Local pickup behaviours and colliders stay disabled so only the server-owned network object can be collected.
- Server seed identities were random on every extraction, which made checkpoints fail strict identity validation and accumulated obsolete test identities. The extracted seed is deterministic, and only current `person.player.*` dynamic identities are restored from checkpoint envelopes.
- Dedicated-server builds retained `OnGUI` and runtime debug-panel behaviors. Extraction and server initialization now remove/avoid those presentation-only components.
- Headless server frames were uncapped. The server now runs in the background at a 60 FPS simulation budget with vSync disabled.
- Multiple local graphical clients were uncapped and could consume enough render time to miss transport heartbeats. Standalone clients now run in the background with a 60 FPS render budget.
- The network simulation still used a 30 Hz tick, local authoritative movement retained remote-player interpolation, and inventory event redraws could wait 100 ms. Client/server networking and dedicated-server physics now run at 60 Hz, interpolation is disabled only for the locally owned actor, and inventory redraws coalesce on the next frame.
- `uig client <id> end` force-terminated the process, skipping the network disconnect handshake. It now requests an orderly window close with a five-second force-stop fallback, while `LocalGameClient` explicitly disconnects during application quit.
- Old world checkpoints containing prototype/test history were removed with user authorization. A fresh checkpoint was saved and restored successfully with no dynamic identities or orphan warnings.

## Automated validation

`VisualGameplayPipelineValidation` and its EditMode tests validate:

- enabled build scenes and transitive asset dependencies;
- missing YAML GUIDs and missing scripts;
- prefab integrity;
- material and shader validity;
- animator controller and animation clip references;
- Prototype Scene camera, visible mesh/material, CanvasScaler, and RectTransform state;
- the required rounded runtime UI surface resource;
- absence of client-only rendering behavior from the synchronized server scene through server boundary tests.

The validator is available from the Unity editor tools menu and is also executed by the test suite.

## Final verification results

- Client EditMode: **1,444 passed, 0 failed, 0 skipped** (`Logs/Runtime/client-full-after-inventory-drag.xml`).
- Server EditMode: **10 passed, 0 failed, 0 skipped** (`Logs/Runtime/server-full-after-inventory-drag.xml`).
- Windows client build: succeeded (`Logs/Runtime/client-build.log`).
- Windows dedicated-server build: succeeded (`Logs/Runtime/server-build.log`).
- Inventory, combat, and narrative authority smokes: completed successfully in their corresponding `*-final.log` files under `Logs/Runtime`.
- Final clean world checkpoint: saved and restored without dynamic test identities, orphan pruning, gameplay errors, or warnings (`Logs/Runtime/server-final-clean-restart.log`).
- Repository whitespace validation: `git diff --check` clean after scene synchronization and server build.

## Runtime paths verified

- clean server start, listen, autosave, world-checkpoint save, and restart/load;
- one long-running client beyond transport timeout;
- two simultaneous clients, orderly disconnect of one, continued operation of the other, and a replacement client joining;
- authoritative movement/collision through the synchronized server scene;
- server-authoritative inventory drag to empty slots, compatible-stack merging, occupied-slot swapping, drop, replicated world pickup, equip, unequip, and consumable use (`Logs/Runtime/client-inventory-drag-smoke.log`);
- repeated single-item drops rejoining the original definition stack without retaining temporary world IDs (`Logs/Runtime/client-stack-recollection-tests.xml` and `Logs/Runtime/client-stack-smoke.log`);
- 60 Hz authoritative movement observed by a separate client process (`Logs/Runtime/client-movement-latency-smoke.log`);
- authored pickup presentation for scene items and renderer-only presentation for dynamic server-authoritative drops, with no fallback-marker warnings (`Logs/Runtime/client-pickup-visual-smoke.log`);
- spell projectile presentation, mana cost, and authoritative target damage;
- authoritative interaction, quest-source browsing, and party creation;
- inventory details, equipment comparison hover, Character, Spells, Journal, Save/Load, Party/navigation layout, HUD, and return transitions;
- 1024x768 and 1600x900 standalone Windows/D3D12 captures without clipping or missing UI surfaces.

Final capture evidence is under:

- `Logs/VisualAudit/1024x768-final-verified`
- `Logs/VisualAudit/1600x900-final-verified`

## Known limits

- The dedicated server intentionally has no rendered viewport; it is verified through scene-boundary validation, collision extraction counts, runtime logs, and authoritative smoke clients.
- Runtime rendering was exercised locally on Windows/D3D12. Other desktop graphics APIs and non-Windows platforms still require platform-specific build/runtime coverage.
- Reversible terrain-layer painting is designed in `Documentation/World` but is not yet implemented. Current terrain appearance remains conventional authored terrain data.
- The capture harness covers every currently implemented main tab and the key inventory hover/detail states. Future modal screens must be added to the harness as they are introduced.
