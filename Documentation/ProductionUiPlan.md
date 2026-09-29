# Production UI Plan

## Product boundary

Phase 4 develops the real player interface. `Projects/Client/Assets/_Project/Scenes/Prototype` remains a development integration scene until the first production world scene is authored, and `Projects/Client/Assets/_Project/Prototype` remains a holding area for temporary world art and prefabs. New reusable UI code belongs under `Packages/com.thequantifier.isekai.client/Runtime/UI`; production presentation assets belong under `Projects/Client/Assets/_Project/Presentation`; configuration belongs under `Projects/Client/Assets/_Project/Configuration`.

Prototype-only world assets are not promoted merely by moving them. They move into production folders during Phase 5 only after their visuals, licensing, performance, naming, and reuse expectations are accepted.

## Visual direction

- Medieval tavern palette built from dark timber brown, leather brown, parchment, brass, and muted gold.
- Rounded panels, buttons, inputs, toggles, and scroll handles form the default silhouette.
- Gold is reserved for titles, focus, selection, and important actions rather than used as a general background.
- Destructive actions use a restrained oxblood red; positive actions use muted olive so both remain compatible with the warm palette.
- Text maintains strong contrast against every brown surface and does not rely on color alone to communicate state.

## Implementation groups

1. Production theme, canvas scaling, common controls, input ownership, and UI audit.
2. HUD resources, interaction prompts, targets, hotbar, quest tracking, and notifications.
3. Main menu shell, navigation persistence, inventory, equipment, and item details.
4. Character, skills, spells, journal, quests, party, and map foundation.
5. Dialogue, guild, trade, crafting, loot, containers, and travel interfaces.
6. Accessibility, responsive-layout validation, controller navigation, performance, and final polish.

## Engineering rules

- Screens consume real gameplay services and definitions; mock data is limited to isolated tests.
- Hidden screens do not rebuild or poll.
- Repeated rows and slots are reused where practical.
- Gameplay input is suppressed while a modal interface owns focus.
- Mouse capture is controlled by the central menu/input policy rather than individual windows.
- Every production screen must be usable at the supported minimum resolution without clipping.
- New screens include state and navigation tests before their implementation group is closed.

## Phase 4 first-group completion criteria

- No production UI class is named as a prototype theme.
- All screen-space canvases use resolution-independent scaling.
- Buttons and common controls share the brown-and-gold rounded visual language.
- Existing inventory, character, spell, journal, party, save/load, dialogue, guild, market, crafting, travel, and prompt interfaces compile against the production theme.
- Theme behavior has focused EditMode coverage and the complete EditMode and PlayMode suites remain green.

The concrete surface inventory, ownership decisions, and migration targets are maintained in `ProductionUiAudit.md`.

## Group 2 implementation status

Group 2 replaces the prototype HUD presentation while retaining the existing gameplay authorities:

- `PlayerVitalsHudView` renders event-driven health, stamina, and mana bars.
- `SpellLoadoutHudView` renders four readable quick slots with a persistent gold selection state.
- `InteractionPromptPresenter` only changes the prompt when focus, text, or menu suppression changes.
- `QuestTrackerHudView` follows the active journal entry and refreshes from narrative change events.
- `CombatTargetHudView` performs the single necessary non-allocating crosshair query and subscribes to the selected target's health event.
- `GameHudNotificationView` provides a bounded, reusable notification queue with information, success, warning, and danger tones.
- `ProductionHudAuthoring` builds the production layout deterministically into the integration scene and can be rerun safely.

The HUD uses no independent gameplay state. Hidden or unchanged elements do not rebuild each frame, all canvases retain the shared safe-area and scale behavior, and the authored scene is covered by EditMode structure tests.

## Group 1-2 polish closeout

- Shared panels, controls, item slots, and HUD frames use warm inset leather surfaces, rounded corners, restrained outlines, and gold focus accents instead of cool gray placeholders.
- The Tab menu uses a vertical navigation stack attached flush to the main window's right edge, with a slight overlap that removes any floating gap. Its Inventory page reserves two thirds for an independently scrolling, auto-wrapping slot grid and one third for a fixed item inspector.
- The inspector keeps artwork, a description-and-stat scroll region, and a compact icon action tray in separate vertical bands. A closed-fist button performs the context-appropriate Use or Equip action, a red curved-arrow drops one item, and a second curved-arrow marked ALL appears only for stacks larger than one and drops the full stack. Only the description band scrolls; the action tray auto-wraps when narrow.
- Equipped items remain represented in the inventory grid as equipped proxies with a gold fist marker. Selecting one changes the primary action to an open-hand Unequip icon; the underlying item identity remains authoritative in the equipment system instead of being duplicated across inventory and equipment persistence.
- Hovering the closed-fist Equip action temporarily replaces the description text with a same-slot comparison against the currently equipped item. It lists current values and desired-minus-current changes for every modifier present on either item, including core stat bonuses, weapon attributes, and typed resistances, while omitting properties that are zero or absent on both. Green marks improvements, red marks reductions, and gold marks no change; moving off the action restores the normal item description.
- Inventory slots remain square at every responsive width. Empty slots are dim brown squares with a centered in-slot "Empty" label, while occupied, hovered, selected, and rarity-accented states are visually distinct.
- Responsive tests cover one through four inventory columns and one through three action columns, and keyboard row navigation follows the live inventory column count.
