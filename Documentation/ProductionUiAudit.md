# Production UI Surface Audit

This audit is the Phase 4 baseline for the player interface. The prototype scene remains the development integration harness, while reusable UI behavior lives under `Assets/_Project/Runtime/UI`, shared styling under `Assets/_Project/Runtime/Presentation`, and production presentation assets under `Assets/_Project/Presentation`.

## Shared infrastructure

| Concern | Production owner | Current rule |
| --- | --- | --- |
| Visual language | `GameUiTheme` and `GameUiThemeApplicator` | Brown, leather, parchment, and gold palette with rounded sliced controls. |
| Canvas scaling | `GameUiTheme.ConfigureCanvas` | Screen-space canvases use `ScaleWithScreenSize` and aspect-aware width/height matching. |
| Safe area | `GameUiSafeArea` | Direct canvas content roots retain their authored anchors inside the platform safe area. |
| Mouse and menu focus | `PlayerCursorMode` | Open menus form an ordered stack. The top menu receives Cancel and any open menu unlocks the cursor. |
| Gameplay input suppression | `PlayerInputReader` | Menu owners block gameplay action queues until every registered owner closes. |
| Narrative gameplay pause | `GameUiModalState` | Dialogue and narrative flows can pause interactions and hostile behavior independently of cursor focus. |

## Player-facing surfaces

| Surface | Current implementation | Data owner | Phase 4 disposition |
| --- | --- | --- | --- |
| Resource HUD | Authored uGUI readouts | Health, stamina, and mana runtime components | Rebuild and polish in Group 2. |
| Hotbar and spell quick slots | Authored uGUI with `SpellQuickSlotView` | Spell loadout and input services | Rebuild and polish in Group 2. |
| Interaction prompt | `InteractionPromptPresenter` and `InteractionPromptView` | Interaction focus service | Rebuild and polish in Group 2. |
| Inventory and equipment | `InventoryScreenController` and `InventoryScreenView` | Inventory and equipment components | Production uGUI foundation; finish in Group 3. |
| Character details | Inventory menu section | Stats, attributes, skills, traits, and status effects | Finish in Groups 3 and 4. |
| Spells | Inventory menu section | Spell loadout and known spells | Finish in Group 4. |
| Quest journal | Inventory menu section | Narrative coordinator | Finish in Group 4. |
| Party | `PartyMenuExtension` | Party runtime components | Finish in Group 4. |
| Save/load | `SaveLoadMenuView` | Persistence service | Finish in Group 3. |
| Dialogue and quest offers | Immediate-mode narrative panels | Narrative coordinator | Replace with reusable uGUI screens in Group 5. |
| Guild desk | Immediate-mode guild panel | Organization membership and quest services | Replace with reusable uGUI screen in Group 5. |
| Market | Immediate-mode market panel | Economy and inventory services | Replace with reusable uGUI screen in Group 5. |
| Crafting | Immediate-mode crafting workstation | Crafting, inventory, and definition services | Replace with reusable uGUI screen in Group 5. |
| Professions | Immediate-mode profession panel | Profession progression services | Integrate into character UI in Group 4. |
| Travel | Immediate-mode travel panel | World location and travel services | Replace with reusable uGUI screen in Group 5. |
| Text chat | Immediate-mode text chat panel | Local chat message state | Preserve behavior; production presentation follows the social UI milestone. |

## Development-only surfaces

`PrototypeSocialDebugPanel`, the Test Lab, and test-only status controls remain development tools. They must not become dependencies of production screens or ship enabled in a release scene. The prototype scene may continue to host them for integration testing.

## Audit decisions

- There is one production visual theme and one cursor/menu ownership policy.
- No second inventory, journal, dialogue, or party data model will be introduced for presentation work.
- Immediate-mode player screens are migration targets, not parallel permanent implementations.
- New production screens must register a close callback with the menu stack, avoid polling Escape independently, and stop expensive refresh work while hidden.
- World art stays in prototype folders until Phase 5 acceptance; moving an asset alone does not make it production-ready.
